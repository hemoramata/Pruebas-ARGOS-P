"use strict";
const el = id => document.getElementById(id);
let challenge = null;
let recovery = null;
let busy = false;
function show(step, message) {
  for (const id of ["login", "verify", "change", "recover", "reset"]) el(id).hidden = id !== step;
  el("forgot").hidden = step !== "login";
  el("cancel").hidden = step === "login";
  el("message").textContent = message;
}
async function post(path, body) {
  const response = await fetch("/api/session/" + path, {method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify(body)});
  if (!response.ok) {
    let data = null;
    try { data = await response.json(); } catch {}
    throw new Error(data?.detail || ({401:"No se pudo verificar el acceso. Revise los datos o solicite un nuevo código.",403:"La cuenta no tiene autorización para esta operación.",429:"Demasiados intentos. Espere un minuto.",503:"El servicio de verificación no está disponible."}[response.status]) || "No se pudo completar la operación.");
  }
  return response.status === 204 ? null : response.json();
}
async function run(operation) {
  if (busy) return;
  busy = true;
  document.querySelectorAll("button").forEach(b => b.disabled = true);
  try { await operation(); } catch (error) { el("message").textContent = error.message; }
  finally { busy = false; document.querySelectorAll("button").forEach(b => b.disabled = false); }
}
el("login").addEventListener("submit", e => { e.preventDefault(); run(async () => {
  if (location.protocol !== "https:") throw new Error("Abra ARGOS mediante HTTPS para iniciar sesión.");
  try { challenge = await post("begin", {identification:el("identification").value, password:el("password").value}); }
  finally { el("password").value = ""; }
  show("verify", "Ingrese el código enviado a " + challenge.maskedDestination + "."); el("code").focus();
}); });
el("verify").addEventListener("submit", e => { e.preventDefault(); run(async () => {
  let account;
  try { account = await post("verify", {id:challenge.id, binding:challenge.binding, code:el("code").value}); }
  finally { el("code").value = ""; }
  challenge = null;
  if (account.mustChangePassword) { show("change", "Acceso verificado."); el("current").focus(); }
  else location.assign("/home/");
}); });
el("change").addEventListener("submit", e => { e.preventDefault(); run(async () => {
  if (el("new").value !== el("repeat").value) throw new Error("Las contraseñas nuevas no coinciden.");
  try { await post("password/change", {currentPassword:el("current").value, newPassword:el("new").value}); }
  finally { for (const id of ["current","new","repeat"]) el(id).value = ""; }
  show("login", "Contraseña cambiada. Ingrese de nuevo con la nueva contraseña.");
}); });
el("cancel").addEventListener("click", () => run(async () => {
  if (recovery) await post("recovery/cancel", {id:recovery.id, binding:recovery.binding});
  recovery = null;
  if (challenge) await post("cancel", {id:challenge.id, binding:challenge.binding});
  await post("logout", {});
  challenge = null;
  for (const id of ["code","password","current","new","repeat","recovery-code","recovery-password","recovery-repeat"]) el(id).value = "";
  show("login", "Sesión cancelada.");
}));
el("forgot").addEventListener("click", () => run(async () => {
  await post("logout", {});
  el("password").value = "";
  el("recovery-identification").value = el("identification").value;
  show("recover", "Recupere el acceso con su identificación y su correo verificado.");
  el("recovery-identification").focus();
}));
el("recover").addEventListener("submit", e => { e.preventDefault(); run(async () => {
  if (location.protocol !== "https:") throw new Error("Abra ARGOS mediante HTTPS.");
  recovery = await post("recovery/begin", {identification:el("recovery-identification").value});
  show("reset", "Si la identificación corresponde a una cuenta habilitada con correo verificado, recibirá un código. Revise también el correo no deseado.");
  el("recovery-code").focus();
}); });
el("reset").addEventListener("submit", e => { e.preventDefault(); run(async () => {
  if (el("recovery-password").value !== el("recovery-repeat").value) throw new Error("Las contraseñas nuevas no coinciden.");
  try { await post("recovery/complete", {id:recovery.id,binding:recovery.binding,code:el("recovery-code").value,newPassword:el("recovery-password").value}); }
  finally { for (const id of ["recovery-code","recovery-password","recovery-repeat"]) el(id).value = ""; }
  recovery = null;
  show("login", "Contraseña restablecida. Las sesiones anteriores quedaron invalidadas. Ingrese con su identificación y la nueva contraseña.");
}); });
