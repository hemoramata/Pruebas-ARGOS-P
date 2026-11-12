"use strict";
const el = id => document.getElementById(id);
const ui = globalThis.ProcedureUI;
const statuses = {Draft:"Borrador",InReview:"En revisión",Approved:"Aprobado",Published:"Publicado",Rejected:"Rechazado",Retired:"Retirado",Archived:"Archivado"};
let authority, selected, choices = [], pending = null, busy = false;
function clearPending() { pending = null; el("code").value = ""; el("confirmation").hidden = true; }
function lock() {
  el("create-fields").disabled = busy || !!pending;
  el("revision-fields").disabled = busy || !!pending;
  el("decision-fields").disabled = busy || !!pending || !choices.length;
  el("confirmation-fields").disabled = busy;
  el("refresh").disabled = busy || !!pending;
  el("logout").disabled = busy;
  el("list").querySelectorAll("button").forEach(b => b.disabled = busy || !!pending);
}
async function api(path, body) {
  const response = await fetch(path, {cache:"no-store",credentials:"same-origin",...(body === undefined ? {} : {
    method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify(body)})});
  if (!response.ok) {
    if (response.status === 401) {
      clearPending(); el("workspace").hidden = true; el("login-link").hidden = false;
      el("list").replaceChildren(); el("history").replaceChildren(); selected = null; authority = null;
    }
    throw Error(({401:"La sesión terminó. Ingrese de nuevo.",403:"No se autorizó la operación. Revise sus atribuciones o solicite un código nuevo.",
      404:"El trámite no está disponible para su institución.",409:"El trámite cambió o la decisión no corresponde a su estado. Actualice el listado.",
      429:"Espere un minuto antes de volver a solicitar un código.",503:"El servicio de verificación no está disponible.",400:"Revise los datos y las reglas de la decisión."})[response.status] || "No se pudo completar la operación. Actualice antes de repetirla.");
  }
  return response.status === 204 ? null : response.json();
}
async function run(work) {
  if (busy) return;
  busy = true; lock();
  try { await work(); } catch (error) { el("message").textContent = error.message; }
  finally { busy = false; lock(); }
}
function select(procedure) {
  if (pending) return;
  selected = procedure; el("detail").hidden = false;
  el("title").textContent = procedure.code + " · " + procedure.name;
  el("state").textContent = "Versión " + procedure.version + " · " + (statuses[procedure.status] || procedure.status);
  el("description").textContent = procedure.description;
  el("revision-panel").hidden = !["Published","Rejected"].includes(procedure.status) || !!procedure.nextVersionId;
  el("revision-name").value = procedure.name;
  el("revision-description").value = procedure.description;
  el("comments").value = ""; el("deadline").value = "";
  const policy = ui.policyFor(procedure, authority.policies);
  el("policy").textContent = policy ? "Política institucional v" + policy.version + " · Aprobaciones requeridas: " + policy.rules.requiredApprovalCount : "Este trámite aún no tiene una política institucional publicada. No se habilitan decisiones.";
  choices = ui.options(procedure, authority);
  el("action").replaceChildren();
  choices.forEach((choice,index) => {
    const option = document.createElement("option"); option.value = String(index);
    const position = authority.positions.find(p => p.id === choice.position);
    option.textContent = ui.labels[choice.action] + " · " + (position?.name || choice.position) + " · " + choice.competence;
    el("action").append(option);
  });
  el("no-authority").textContent = choices.length ? "El servidor comprobará nuevamente sus atribuciones al confirmar." : "No hay decisiones habilitadas para sus asignaciones y el estado de esta versión.";
  updateDeadline(); el("history").replaceChildren();
  for (const decision of procedure.decisions || []) {
    const entry = document.createElement("li");
    entry.textContent = decision.type + " · " + new Date(decision.occurredAtUtc).toLocaleString("es") + " · Persona: " + decision.actorReference + " · Asignación: " + decision.assignmentReference + " · Autorización: " + decision.authorizationReference + (decision.comments ? " · " + decision.comments : "");
    el("history").append(entry);
  }
  if (!(procedure.decisions || []).length) { const entry = document.createElement("li"); entry.textContent = "Sin decisiones registradas."; el("history").append(entry); }
  lock();
}
function updateDeadline() { const emergency = choices[Number(el("action").value)]?.action === "publish-emergency"; el("deadline-label").hidden = !emergency; el("deadline").required = emergency; }
async function load() {
  clearPending();
  authority = await api("/api/procedures/authority-options");
  el("setup-link").hidden = !(authority.permissions || []).includes("personnel.manage");
  const procedures = await api("/api/procedures/administration");
  el("list").replaceChildren(); el("workspace").hidden = false; el("detail").hidden = true; choices = [];
  for (const procedure of procedures) {
    const button = document.createElement("button"); button.type = "button";
    button.textContent = procedure.code + " · " + procedure.name + " · v" + procedure.version + " · " + (statuses[procedure.status] || procedure.status);
    button.addEventListener("click", () => { if (!busy) select(procedure); }); el("list").append(button);
  }
  const previous = procedures.find(p => p.id === selected?.id); if (previous) select(previous);
  el("message").textContent = procedures.length ? "Seleccione una versión para consultar su historial y decisiones disponibles." : "No hay trámites registrados para esta institución.";
}
el("action").addEventListener("change", updateDeadline);
el("refresh").addEventListener("click", () => run(load));
el("decision").addEventListener("submit", event => { event.preventDefault(); run(async () => {
  if (!selected || pending) return;
  const snapshot = ui.prepare(selected.id, choices[Number(el("action").value)], el("comments").value, el("deadline").value);
  const challenge = await api(snapshot.path + "/verification", snapshot.request);
  pending = {snapshot,challenge}; el("confirmation").hidden = false;
  el("summary").textContent = ui.labels[snapshot.action] + " · " + selected.code + " · versión " + selected.version + ". Los datos quedan fijados hasta confirmar o cancelar.";
  el("destination").textContent = "Código enviado a " + challenge.maskedDestination + ". Vence: " + new Date(challenge.expiresAt).toLocaleTimeString("es");
  el("message").textContent = "Revise la decisión antes de confirmar. No comparta el código.";
}); });
el("confirmation").addEventListener("submit", event => { event.preventDefault(); run(async () => {
  if (!pending) return;
  const current = pending;
  try { const body = ui.confirmation(current.snapshot,current.challenge,el("code").value); clearPending(); await api(current.snapshot.path,body); await load(); el("message").textContent = "Decisión registrada. El historial fue actualizado."; }
  finally { clearPending(); }
}); });
el("cancel").addEventListener("click", () => { if (!busy) { clearPending(); lock(); el("message").textContent = "Confirmación descartada en esta pantalla. No se ejecutó la decisión; el código emitido caducará."; } });
el("create").addEventListener("submit", event => { event.preventDefault(); run(async () => {
  await api("/api/procedures/",{code:el("new-code").value,name:el("new-name").value,description:el("new-description").value});
  el("create").reset(); await load(); el("message").textContent = "Borrador creado. Aún no está publicado ni disponible para operar.";
}); });
el("logout").addEventListener("click", () => run(async () => { clearPending(); await api("/api/session/logout",{}); location.assign("/login/"); }));
el("revision").addEventListener("submit", event => { event.preventDefault(); run(async () => {
  if (!selected || pending || !["Published","Rejected"].includes(selected.status) || selected.nextVersionId) return;
  const created = await api("/api/procedures/" + encodeURIComponent(selected.id) + "/versions", {
    name:el("revision-name").value,description:el("revision-description").value});
  selected = created; await load();
  el("message").textContent = "Nueva versión creada como borrador. La versión anterior y su historial se conservan.";
}); });
window.addEventListener("pagehide", () => { clearPending(); el("comments").value = ""; el("workspace").hidden = true; el("list").replaceChildren(); el("history").replaceChildren(); });
window.addEventListener("pageshow", event => { if (event.persisted) run(load); });
run(async () => { if (location.protocol !== "https:") throw Error("Abra ARGOS mediante HTTPS para utilizar la sesión."); await load(); });
