using System.Security.Claims;
using ARGOS.Domain.Aggregates.CaseFile;
using ARGOS.Domain.Aggregates.CaseFile.ValueObjects;
using ARGOS.Domain.Aggregates.Evidence;
using ARGOS.Domain.Aggregates.Evidence.ValueObjects;
using ARGOS.Infrastructure.Persistence;
using ARGOS.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace ARGOS.API.Endpoints;

/// <summary>
/// Operaciones mínimas para registrar expedientes y evidencia institucional reutilizable.
/// La autorización se deriva de las competencias de la posición institucional.
/// </summary>
public static class CaseFileEndpoints
{
    public static IEndpointRouteBuilder MapCaseFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var cases = endpoints.MapGroup("/api/case-files").WithTags("Case files").RequireAuthorization();
        cases.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (!Guid.TryParse(context.HttpContext.User.FindFirstValue("organization_id"), out var organizationId)
                || organizationId == Guid.Empty) return Results.Forbid();
            if (context.HttpContext.Request.RouteValues.TryGetValue("action", out var action)
                && action?.ToString() is "finalize" or "archive"
                && !context.HttpContext.User.HasClaim("permission", CaseActionVerification.Permission(action!.ToString()!)))
                return Results.Forbid();
            if (context.HttpContext.Request.RouteValues.TryGetValue("id", out var routeId)
                && Guid.TryParse(routeId?.ToString(), out var caseId))
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<ArgosDbContext>();
                if (!await db.AuditEntries.AnyAsync(x => x.OrganizationId == organizationId
                    && x.ActionCode == "CaseFile.Created" && x.SubjectType == "CaseFile"
                    && x.SubjectReference == caseId.ToString() && x.Outcome == "Succeeded",
                    context.HttpContext.RequestAborted)) return Results.NotFound();
            }
            try { return await next(context); }
            catch (DbUpdateConcurrencyException)
            { return Results.Conflict(new { code = "CaseFile.ConcurrentChange", message = "El expediente cambió. Actualice la consulta antes de continuar." }); }
            catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException
                { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_CaseFiles_Number" })
            { return Results.Conflict(new { code = "CaseFile.DuplicateNumber" }); }
        });

        cases.MapGet("/", async (HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var organizationId = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            var owned = db.AuditEntries.Where(x => x.OrganizationId == organizationId && x.ActionCode == "CaseFile.Created"
                && x.SubjectType == "CaseFile" && x.Outcome == "Succeeded").Select(x => x.SubjectReference);
            var items = await db.CaseFiles.AsNoTracking()
                .Where(x => owned.Contains(x.Id.ToString()))
                .OrderByDescending(x => x.Id)
                .ToListAsync(ct);
            return Results.Ok(items.Select(x => new
            {
                x.Id, Number = x.Number.Value, x.Status, x.WaitingReason, x.FinalResultCode
            }));
        }).WithName("ListCaseFiles").RequireAuthorization("case-files.read");

        cases.MapGet("/{id:guid}", async (Guid id, ArgosDbContext db, CancellationToken ct) =>
        {
            var item = await db.CaseFiles.AsNoTracking()
                .Include(x => x.EvidenceLinks)
                .SingleOrDefaultAsync(x => x.Id == id, ct);
            return item is null ? Results.NotFound() : Results.Ok(new
            {
                item.Id,
                Number = item.Number.Value,
                item.Status,
                item.WaitingReason,
                item.FinalResultCode,
                Evidence = item.EvidenceLinks.Select(x => new
                {
                    x.Id,
                    EvidenceId = x.EvidenceId.Value,
                    x.RequirementCode,
                    x.ProcedureVersionId,
                    x.PolicyVersion,
                    x.LinkedByPositionId,
                    x.ValidatedAtUtc,
                    x.LinkedAtUtc
                })
            });
        }).WithName("GetCaseFile").RequireAuthorization("case-files.read");

        cases.MapPost("/", async (CreateCaseFileRequest request, HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var number = CaseFileNumber.Create(request.Number);
            if (number.IsFailure)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["number"] = [number.Error.Description] });

            if (await db.CaseFiles.AnyAsync(x => x.Number == number.Value, ct))
                return Results.Conflict(new { code = "CaseFile.DuplicateNumber" });

            CaseFile item;
            try { item = new CaseFile(Guid.NewGuid(), number.Value, request.SubjectTypeCode, request.SubjectReference, request.ProcedureVersionId); }
            catch (ArgumentException) { return Results.BadRequest(new { code = "CaseFile.InvalidSubject" }); }
            if (request.ProcedureVersionId is { } procedureId)
            {
                var procedure = await db.Procedures.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ARGOS.Domain.Aggregates.Procedure.ValueObjects.ProcedureId.From(procedureId), ct);
                var organization = Guid.Parse(http.User.FindFirstValue("organization_id")!);
                if (procedure?.Status != ARGOS.Domain.Aggregates.Procedure.Enums.ProcedureStatus.Published
                    || !Guid.TryParse(procedure.ApprovalPolicyVersion, out var policyId)
                    || !await db.OperationPolicies.AnyAsync(x => x.Id == policyId && x.OrganizationId == organization, ct))
                    return Results.BadRequest(new { code = "CaseFile.PublishedInstitutionalProcedureRequired" });
            }
            db.CaseFiles.Add(item);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.Created", "CaseFile", item.Id.ToString(), "Succeeded", item.Number.Value));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/case-files/{item.Id}", new { item.Id, Number = item.Number.Value, item.Status });
        }).WithName("CreateCaseFile").RequireAuthorization("case-files.create");

        cases.MapPost("/{id:guid}/content", async (Guid id, StoreProtectedContentRequest request,
            HttpContext http, ArgosDbContext db, IEnvelopeEncryptionProvider encryption, CancellationToken ct) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var caseFile = await db.CaseFiles.FromSqlInterpolated($"SELECT * FROM \"CaseFiles\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (caseFile is null) return Results.NotFound();
            if (caseFile.Status is ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Finalized
                or ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Archived)
                return Results.Conflict(new { code = "CaseFile.ContentNotAllowed" });
            if (string.IsNullOrWhiteSpace(request.ContentType) || string.IsNullOrWhiteSpace(request.Base64Content)
                || request.ContentType.Trim().Length > 200
                || request.Base64Content.Length > 14_000_000
                || !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(request.ContentType, out _))
                return Results.BadRequest(new { code = "CaseFile.ContentInvalid" });
            byte[] plain;
            try { plain = Convert.FromBase64String(request.Base64Content); }
            catch (FormatException) { return Results.BadRequest(new { code = "CaseFile.ContentInvalid" }); }
            EncryptedEnvelope envelope;
            try { envelope = await encryption.EncryptAsync(plain, $"CaseFile:{id:D}", ct); }
            catch (InvalidOperationException) { return Results.StatusCode(503); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain); }
            var content = ProtectedCaseContent.Create(id, request.ContentType.Trim(), envelope, DateTimeOffset.UtcNow);
            db.ProtectedCaseContents.Add(content);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.ContentStored", "ProtectedCaseContent", content.Id.ToString(),
                "Succeeded", $"case:{id:D}"));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/case-files/{id}/content/{content.Id}", new { content.Id, content.ContentType, content.KeyVersion });
        }).WithName("StoreProtectedCaseContent").RequireAuthorization("case-files.update");

        cases.MapPost("/{id:guid}/document-capture", async (Guid id, StoreCapturedDocumentRequest request,
            HttpContext http, ArgosDbContext db, IEnvelopeEncryptionProvider encryption, TimeProvider clock, CancellationToken ct) =>
        {
            if (!Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organization)
                || !Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var person)) return Results.Forbid();
            if (request.FinalCategoryId == Guid.Empty || string.IsNullOrWhiteSpace(request.ContentType)
                || string.IsNullOrWhiteSpace(request.Base64Content) || request.ContentType.Trim().Length > 200
                || request.Base64Content.Length > 14_000_000 || !System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(request.ContentType, out _))
                return Results.BadRequest(new { code = "DocumentCapture.Invalid" });
            byte[] plain;
            try { plain = Convert.FromBase64String(request.Base64Content); }
            catch (FormatException) { return Results.BadRequest(new { code = "DocumentCapture.Invalid" }); }
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var caseFile = await db.CaseFiles.FromSqlInterpolated($"SELECT * FROM \"CaseFiles\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
                if (caseFile is null) return Results.NotFound();
                if (caseFile.Status is ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Finalized or ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Archived)
                    return Results.Conflict(new { code = "CaseFile.ContentNotAllowed" });
                var category = await db.DocumentCategories.SingleOrDefaultAsync(x => x.Id == request.FinalCategoryId && x.OrganizationId == organization && x.IsActive, ct);
                if (category is null) return Results.BadRequest(new { code = "DocumentCapture.FinalCategoryInvalid" });
                if (request.SuggestedCategoryId is { } suggested && !await db.DocumentCategories.AnyAsync(x => x.Id == suggested && x.OrganizationId == organization, ct))
                    return Results.BadRequest(new { code = "DocumentCapture.SuggestedCategoryInvalid" });
                var envelope = await encryption.EncryptAsync(plain, $"CaseFile:{id:D}", ct);
                var content = ProtectedCaseContent.Create(id, request.ContentType.Trim(), envelope, clock.GetUtcNow());
                var decision = request.SuggestedCategoryId is null ? "ManuallyClassified" : request.SuggestedCategoryId == request.FinalCategoryId ? "Approved" : "Corrected";
                var review = DocumentClassificationReview.Create(organization, content.Id, request.SuggestedCategoryId, request.FinalCategoryId,
                    request.SuggestionSource, request.Confidence, decision, person, clock.GetUtcNow());
                db.ProtectedCaseContents.Add(content); db.DocumentClassificationReviews.Add(review);
                db.AuditEntries.Add(CreateAudit(http, "Document.CapturedAndClassified", "ProtectedCaseContent", content.Id.ToString(), "Succeeded",
                    $"case:{id:D};category:{category.Code};decision:{decision};review:{review.Id:D}"));
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
                return Results.Created($"/api/case-files/{id}/content/{content.Id}", new { content.Id, ClassificationReviewId = review.Id, Category = category.Code, Decision = decision });
            }
            catch (ArgumentException error) { return Results.BadRequest(new { code = "DocumentCapture.Invalid", detail = error.Message }); }
            catch (InvalidOperationException) { return Results.StatusCode(503); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain); }
        }).WithName("CaptureAndClassifyDocument").RequireAuthorization("case-files.update");

        cases.MapGet("/{id:guid}/content/{contentId:guid}", async (Guid id, Guid contentId,
            HttpContext http, ArgosDbContext db, IEnvelopeEncryptionProvider encryption, CancellationToken ct) =>
        {
            var content = await db.ProtectedCaseContents.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == contentId && x.CaseFileId == id, ct);
            if (content is null) return Results.NotFound();
            byte[] plain;
            try { plain = await encryption.DecryptAsync(content.EnvelopeFor(id), ct); }
            catch (InvalidOperationException)
            {
                db.AuditEntries.Add(CreateAudit(http, "CaseFile.ContentReadFailed", "ProtectedCaseContent", contentId.ToString(),
                    "Failed", $"case:{id:D}"));
                await db.SaveChangesAsync(ct);
                return Results.StatusCode(503);
            }
            try
            {
                db.AuditEntries.Add(CreateAudit(http, "CaseFile.ContentReadAuthorized", "ProtectedCaseContent", contentId.ToString(),
                    "Succeeded", $"case:{id:D}"));
                await db.SaveChangesAsync(ct);
            }
            catch { System.Security.Cryptography.CryptographicOperations.ZeroMemory(plain); throw; }
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(plain, content.ContentType, fileDownloadName: $"{content.Id:D}.bin");
        }).WithName("ReadProtectedCaseContent").RequireAuthorization("case-files.read");

        cases.MapPost("/{id:guid}/evidence", async (Guid id, LinkEvidenceRequest request, HttpContext http, ArgosDbContext db, EvidenceUseAuthorization authorization, CancellationToken ct) =>
        {
            if (request.EvidenceId == Guid.Empty)
                return Results.BadRequest(new { code = "Evidence.InvalidId" });
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Lock the aggregate before loading its links: inserting a child does not
            // update the parent's concurrency token, so optimistic checks alone are insufficient.
            var item = await db.CaseFiles.FromSqlInterpolated($"SELECT * FROM \"CaseFiles\" WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (item is null) return Results.NotFound(new { code = "CaseFile.NotFound" });
            await db.Entry(item).Collection(x => x.EvidenceLinks).LoadAsync(ct);
            var organizationId = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            var evidenceItem = await db.Evidences.FromSqlInterpolated($"SELECT * FROM \"Evidences\" WHERE \"Id\" = {request.EvidenceId} AND \"CustodianOrganizationId\" = {organizationId} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (evidenceItem is null)
                return Results.NotFound(new { code = "Evidence.NotFound" });
            await db.Entry(evidenceItem).Collection(x => x.Sources).LoadAsync(ct);

            if (item.Status is ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Finalized or ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Archived)
                return Results.Conflict(new { code = "CaseFile.EvidenceLinkNotAllowed" });
            if (string.IsNullOrWhiteSpace(request.RequirementCode) || item.ProcedureVersionId != request.ProcedureVersionId)
                return Results.BadRequest(new { code = "Evidence.InvalidRequirement" });
            var authorized = await authorization.Resolve(http, item, evidenceItem, request.AssignmentId, request.CompetenceCode ?? "", request.RequirementCode, ct);
            if (authorized is null)
            {
                db.AuditEntries.Add(CreateAudit(http, "Evidence.UseRejected", "CaseFile", id.ToString(), "Rejected", $"evidence:{request.EvidenceId}"));
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
                return Results.Json(new { code = "Evidence.UseNotAuthorized" }, statusCode: 403);
            }

            var result = item.LinkEvidence(
                EvidenceId.From(request.EvidenceId),
                request.RequirementCode,
                request.ProcedureVersionId,
                authorized.PolicyId.ToString(),
                authorized.Authority.Position,
                authorized.Now,
                authorized.Now);
            if (result.IsFailure) return Results.Conflict(result.Error);

            db.AuditEntries.Add(CreateAudit(http, "Evidence.LinkedToCaseFile", "CaseFileEvidence", result.Value.Id.ToString(), "Succeeded", $"caseFile:{id};evidence:{request.EvidenceId};requirement:{result.Value.RequirementCode};assignment:{authorized.Authority.Assignment};policy:{authorized.PolicyId}"));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/case-files/{id}/evidence/{result.Value.Id}", new
            {
                result.Value.Id,
                EvidenceId = result.Value.EvidenceId.Value,
                result.Value.RequirementCode,
                result.Value.ValidatedAtUtc,
                result.Value.LinkedAtUtc
            });
        }).WithName("LinkEvidenceToCaseFile").RequireAuthorization("case-files.update");

        var evidence = endpoints.MapGroup("/api/evidences").WithTags("Evidence").RequireAuthorization();
        evidence.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (!Guid.TryParse(context.HttpContext.User.FindFirstValue("organization_id"), out var organizationId)
                || organizationId == Guid.Empty) return Results.Forbid();
            return await next(context);
        });
        evidence.MapGet("/", async (HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var organizationId = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            var items = await db.Evidences.AsNoTracking()
                .Where(x => x.CustodianOrganizationId == organizationId)
                .Include(x => x.Sources)
                .ToListAsync(ct);
            return Results.Ok(items.OrderByDescending(x => x.CreatedAtUtc).Select(x => new
            {
                EvidenceId = x.Id.Value,
                x.CustodianOrganizationId,
                x.EvidenceTypeCode,
                x.SubjectTypeCode,
                x.SubjectReference,
                x.Status,
                x.CreatedAtUtc,
                Sources = x.Sources.Select(source => new
                {
                    SourceId = source.Id.Value,
                    source.SourceTypeCode,
                    source.SourceReference,
                    source.Role,
                    source.ProducedAtUtc,
                    source.ReceivedAtUtc,
                    source.Channel,
                    source.ReceivedByPositionId
                })
            }));
        }).WithName("ListEvidence").RequireAuthorization("evidences.read");

        evidence.MapPost("/", async (CreateEvidenceRequest request, HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var organizationId = Guid.Parse(http.User.FindFirstValue("organization_id")!);
            if (request.CustodianOrganizationId != organizationId) return Results.Forbid();
            if (request.ProtectedContentId is { } contentId)
            {
                var content = await db.ProtectedCaseContents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == contentId, ct);
                if (content is null || !await db.AuditEntries.AnyAsync(x => x.OrganizationId == organizationId
                    && x.ActionCode == "CaseFile.Created" && x.SubjectType == "CaseFile" && x.Outcome == "Succeeded"
                    && x.SubjectReference == content.CaseFileId.ToString(), ct)) return Results.Forbid();
            }
            var source = EvidenceSource.Create(
                request.SourceTypeCode,
                request.SourceReference,
                request.Role,
                request.ProducedAtUtc,
                request.ReceivedAtUtc,
                request.Channel,
                request.ReceivedByPositionId);
            if (source.IsFailure)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["source"] = [source.Error.Description] });

            var item = Evidence.Create(
                request.CustodianOrganizationId,
                request.EvidenceTypeCode,
                request.SubjectTypeCode,
                request.SubjectReference,
                source.Value,
                DateTimeOffset.UtcNow, request.ProtectedContentId);
            if (item.IsFailure)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["evidence"] = [item.Error.Description] });

            db.Evidences.Add(item.Value);
            db.AuditEntries.Add(CreateAudit(http, "Evidence.Created", "Evidence", item.Value.Id.Value.ToString(), "Succeeded", $"type:{item.Value.EvidenceTypeCode};subject:{item.Value.SubjectReference}"));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/evidences/{item.Value.Id.Value}", new
            {
                EvidenceId = item.Value.Id.Value,
                item.Value.EvidenceTypeCode,
                item.Value.SubjectTypeCode,
                item.Value.SubjectReference,
                item.Value.Status
            });
        }).WithName("CreateEvidence").RequireAuthorization("evidences.create");

        cases.MapPost("/{id:guid}/start", async (Guid id, HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var item = await db.CaseFiles.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound(new { code = "CaseFile.NotFound" });
            var result = item.StartProcessing();
            if (result.IsFailure) return Results.Conflict(result.Error);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.Started", "CaseFile", id.ToString(), "Succeeded", null));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { item.Id, item.Status });
        }).WithName("StartCaseFile").RequireAuthorization("case-files.update");

        cases.MapPost("/{id:guid}/hold", async (Guid id, HoldCaseFileRequest request, HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var item = await db.CaseFiles.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound(new { code = "CaseFile.NotFound" });
            if (string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["La razón de espera es obligatoria."] });
            var result = item.PutOnHold(request.Reason);
            if (result.IsFailure) return Results.Conflict(result.Error);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.PutOnHold", "CaseFile", id.ToString(), "Succeeded", request.Reason));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { item.Id, item.Status, item.WaitingReason });
        }).WithName("HoldCaseFile").RequireAuthorization("case-files.update");

        cases.MapPost("/{id:guid}/resume", async (Guid id, HttpContext http, ArgosDbContext db, CancellationToken ct) =>
        {
            var item = await db.CaseFiles.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound(new { code = "CaseFile.NotFound" });
            var result = item.ResumeProcessing();
            if (result.IsFailure) return Results.Conflict(result.Error);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.Resumed", "CaseFile", id.ToString(), "Succeeded", null));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { item.Id, item.Status });
        }).WithName("ResumeCaseFile").RequireAuthorization("case-files.update");

        cases.MapPost("/{id:guid}/finalize", async (Guid id, FinalizeCaseFileRequest request, HttpContext http, ArgosDbContext db, CaseActionVerification verification, CancellationToken ct) =>
        {
            var item = await db.CaseFiles.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound(new { code = "CaseFile.NotFound" });
            if (string.IsNullOrWhiteSpace(request.ResultCode) || request.ResultCode.Trim().Length > 100)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["resultCode"] = ["Indique un resultado de hasta 100 caracteres."] });
            if (item.Status is not (ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.InProgress or ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Waiting))
                return Results.Conflict(new { code = "CaseFile.InvalidStatusTransition" });
            if (!await VerifyAction(http, db, verification, id, "finalize", request.ResultCode, request.ChallengeId, request.Code, ct))
                return Results.Json(new { code = "CaseFile.AdditionalVerificationRequired" }, statusCode: 403);
            var result = item.FinalizeWithResult(request.ResultCode);
            if (result.IsFailure) return Results.Conflict(result.Error);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.Finalized", "CaseFile", id.ToString(), "Succeeded", request.ResultCode));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { item.Id, item.Status, item.FinalResultCode });
        }).WithName("FinalizeCaseFile").RequireAuthorization("case-files.finalize");

        cases.MapPost("/{id:guid}/archive", async (Guid id, ArchiveCaseFileRequest request, HttpContext http, ArgosDbContext db, CaseActionVerification verification, CancellationToken ct) =>
        {
            var item = await db.CaseFiles.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound(new { code = "CaseFile.NotFound" });
            if (item.Status != ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Finalized)
                return Results.Conflict(new { code = "CaseFile.InvalidStatusTransition" });
            if (!await VerifyAction(http, db, verification, id, "archive", null, request.ChallengeId, request.Code, ct))
                return Results.Json(new { code = "CaseFile.AdditionalVerificationRequired" }, statusCode: 403);
            var result = item.Archive();
            if (result.IsFailure) return Results.Conflict(result.Error);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.Archived", "CaseFile", id.ToString(), "Succeeded", null));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { item.Id, item.Status });
        }).WithName("ArchiveCaseFile").RequireAuthorization("case-files.archive");

        cases.MapPost("/{id:guid}/{action}/verification", async (Guid id, string action, ActionVerificationRequest request,
            HttpContext http, ArgosDbContext db, CaseActionVerification verification, CancellationToken ct) =>
        {
            if (action is not ("finalize" or "archive")) return Results.NotFound();
            if (!http.User.HasClaim("permission", CaseActionVerification.Permission(action)))
            {
                db.AuditEntries.Add(CreateAudit(http, "CaseFile.VerificationDenied", "CaseFile", id.ToString(), "Rejected", action));
                await db.SaveChangesAsync(ct);
                return Results.Forbid();
            }
            if (action == "finalize" && (string.IsNullOrWhiteSpace(request.ResultCode) || request.ResultCode.Trim().Length > 100))
                return Results.BadRequest(new { code = "CaseFile.FinalResultRequired" });
            var item = await db.CaseFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound(new { code = "CaseFile.NotFound" });
            if ((action == "finalize" && item.Status is not (ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.InProgress or ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Waiting))
                || (action == "archive" && item.Status != ARGOS.Domain.Aggregates.CaseFile.Enums.CaseFileStatus.Finalized))
                return Results.Conflict(new { code = "CaseFile.InvalidStatusForVerification" });
            var person = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var session = Guid.Parse(http.User.FindFirstValue("session_id")!);
            LoginChallenge? challenge;
            try { challenge = await verification.BeginAsync(person, session, id, action, request.ResultCode, ct); }
            catch (Exception error) when (error is InvalidOperationException or TimeoutException
                || error is OperationCanceledException && !ct.IsCancellationRequested)
            {
                db.AuditEntries.Add(CreateAudit(http, "CaseFile.VerificationDeliveryFailed", "CaseFile", id.ToString(), "Failed", action));
                await db.SaveChangesAsync(ct);
                return Results.StatusCode(503);
            }
            if (challenge is null) return Results.StatusCode(429);
            db.AuditEntries.Add(CreateAudit(http, "CaseFile.VerificationIssued", "CaseFile", id.ToString(),
                "Succeeded", $"action:{action};challenge:{challenge.Id}"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(challenge);
        }).RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa")).RequireRateLimiting("login");

        return endpoints;
    }

    private static async Task<bool> VerifyAction(HttpContext http, ArgosDbContext db, CaseActionVerification verification, Guid id,
        string action, string? resultCode, Guid challengeId, string? code, CancellationToken ct)
    {
        var accepted = await verification.ConsumeAsync(Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Guid.Parse(http.User.FindFirstValue("session_id")!), id, action, resultCode, challengeId, code, ct);
        db.AuditEntries.Add(CreateAudit(http, "CaseFile.VerificationChecked", "CaseFile", id.ToString(),
            accepted ? "Succeeded" : "Rejected", $"action:{action};challenge:{challengeId}"));
        // A successful verification is persisted atomically with the case mutation.
        // Failed verification has no case mutation and must still be recorded.
        if (!accepted) await db.SaveChangesAsync(ct);
        return accepted;
    }

    private static AuditEntry CreateAudit(HttpContext http, string actionCode, string subjectType, string subjectReference, string outcome, string? details)
    {
        var organization = Guid.TryParse(http.User.FindFirstValue("organization_id"), out var organizationId)
            ? organizationId
            : (Guid?)null;
        return AuditEntry.Create(
            organization,
            http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
            http.User.FindFirstValue("session_id"),
            actionCode,
            subjectType,
            subjectReference,
            outcome,
            http.TraceIdentifier,
            details);
    }

    public sealed record CreateCaseFileRequest(string? Number, string? SubjectTypeCode = null, string? SubjectReference = null, Guid? ProcedureVersionId = null);
    public sealed record StoreProtectedContentRequest(string? ContentType, string? Base64Content);
    public sealed record StoreCapturedDocumentRequest(string? ContentType, string? Base64Content, Guid FinalCategoryId,
        Guid? SuggestedCategoryId, string? SuggestionSource, decimal? Confidence);
    public sealed record HoldCaseFileRequest(string? Reason);
    public sealed record ActionVerificationRequest(string? ResultCode = null);
    public sealed record FinalizeCaseFileRequest(string? ResultCode, Guid ChallengeId = default, string? Code = null);
    public sealed record ArchiveCaseFileRequest(Guid ChallengeId = default, string? Code = null);
    public sealed record LinkEvidenceRequest(
        Guid EvidenceId,
        string? RequirementCode,
        Guid ProcedureVersionId,
        string? PolicyVersion,
        Guid LinkedByPositionId,
        DateTimeOffset ValidatedAtUtc,
        DateTimeOffset LinkedAtUtc,
        Guid AssignmentId = default,
        string? CompetenceCode = null);
    public sealed record CreateEvidenceRequest(
        Guid CustodianOrganizationId,
        string? EvidenceTypeCode,
        string? SubjectTypeCode,
        string? SubjectReference,
        string? SourceTypeCode,
        string? SourceReference,
        string? Role,
        DateTimeOffset ProducedAtUtc,
        DateTimeOffset ReceivedAtUtc,
        string? Channel,
        Guid? ReceivedByPositionId,
        DateTimeOffset CreatedAtUtc = default,
        Guid? ProtectedContentId = null);
}
