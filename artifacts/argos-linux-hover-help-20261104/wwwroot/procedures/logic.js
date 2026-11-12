"use strict";
(function (root) {
  const labels = {"submit-review":"Enviar a revisión",approve:"Aprobar",reject:"Rechazar",publish:"Publicar",
    "publish-emergency":"Publicar por emergencia","ratify-emergency":"Ratificar emergencia",retire:"Retirar de vigencia",archive:"Archivar versión"};
  function policyFor(procedure, policies) {
    if (procedure.approvalPolicyVersion) return policies.find(p => p.id === procedure.approvalPolicyVersion && p.procedureCode === procedure.code);
    return policies.filter(p => p.procedureCode === procedure.code).sort((a,b) => b.version-a.version)[0];
  }
  function options(procedure, authority) {
    const policy = policyFor(procedure, authority.policies || []);
    if (!policy) return [];
    const states = {Draft:["submit-review"],InReview:["approve","reject"],Approved:["publish"],Published:["retire"],Rejected:["archive"],Retired:["archive"],Archived:[]};
    const actions = [...(states[procedure.status] || [])];
    if (["InReview","Approved"].includes(procedure.status) && policy.rules.emergencyPublicationAllowed) actions.push("publish-emergency");
    if (procedure.status === "Published" && procedure.emergencyRatificationPending) actions.push("ratify-emergency");
    return (authority.assignments || []).flatMap(assignment => policy.rules.authorities
      .filter(rule => rule.positionId === assignment.positionId && actions.includes(rule.action))
      .map(rule => ({action:rule.action,assignment:assignment.id,position:assignment.positionId,competence:rule.competenceCode,policy:policy.id})));
  }
  function prepare(id, option, comments, deadline) {
    if (!option || !labels[option.action]) throw Error("Seleccione una decisión autorizada.");
    if (comments.length > 2000) throw Error("La observación admite hasta 2000 caracteres.");
    const request = {decision:{assignmentReference:option.assignment,competenceCode:option.competence,policyId:option.policy,comments}};
    if (option.action === "publish-emergency") {
      const time = Date.parse(deadline);
      if (!Number.isFinite(time) || time <= Date.now()) throw Error("Indique una fecha futura para la ratificación.");
      request.ratificationDueAtUtc = new Date(time).toISOString();
    }
    return {path:"/api/procedures/"+encodeURIComponent(id)+"/"+option.action,action:option.action,request};
  }
  function confirmation(snapshot, challenge, code) {
    if (!challenge || !Number.isFinite(Date.parse(challenge.expiresAt)) || Date.parse(challenge.expiresAt) <= Date.now()) throw Error("El código venció. Solicite uno nuevo.");
    if (!/^\d{6}$/.test(code)) throw Error("Ingrese los seis dígitos del código.");
    const request = JSON.parse(JSON.stringify(snapshot.request));
    request.decision.challengeId = challenge.id; request.decision.code = code;
    return snapshot.action === "submit-review" || snapshot.action === "publish-emergency" ? request : request.decision;
  }
  const api = {labels,policyFor,options,prepare,confirmation};
  if (typeof module !== "undefined") module.exports = api; else root.ProcedureUI = api;
})(globalThis);
