"use strict";
const el = id => document.getElementById(id);
let challenge = null;
let recovery = null;
let busy = false;
let recoveryResendAt = 0;
let recoveryResendTimer = null;
function setMessage(message, kind = "info") {
  const messageElement = el("message");
  messageElement.textContent = message || "";
  messageElement.className = message ? kind : "";
}
function scheduleRecoveryResend() {
  const button = el("resend-recovery");
  const detail = el("recovery-cooldown");
  if (recoveryResendTimer) clearInterval(recoveryResendTimer);
  const update = () => {
    const seconds = Math.max(0, Math.ceil((recoveryResendAt - Date.now()) / 1000));
    button.disabled = seconds > 0 || busy;
    button.textContent = seconds > 0 ? `Solicitar un código nuevo (${seconds}s)` : "Solicitar un código nuevo";
    detail.textContent = seconds > 0
      ? `Podrá solicitar otro código en ${seconds} segundos. El código actual vence en tres minutos.`
      : "Puede solicitar un código nuevo. El código anterior dejará de ser válido.";
    if (seconds === 0 && recoveryResendTimer) { clearInterval(recoveryResendTimer); recoveryResendTimer = null; }
  };
  update();
  if (Date.now() < recoveryResendAt) recoveryResendTimer = setInterval(update, 1000);
}
function installPasswordToggles() {
  document.querySelectorAll('input[type="password"]').forEach(input => {
    const wrapper = document.createElement("span");
    wrapper.className = "password-field";
    input.parentNode.insertBefore(wrapper, input);
    wrapper.append(input);
    const button = document.createElement("button");
    button.type = "button";
    button.className = "password-toggle";
    button.setAttribute("aria-label", "Mostrar contraseña");
    button.setAttribute("aria-pressed", "false");
    button.innerHTML = '<span class="argos-eye-mark" aria-hidden="true">A</span>';
    button.addEventListener("click", () => {
      const visible = input.type === "text";
      input.type = visible ? "password" : "text";
      button.classList.toggle("revealed", !visible);
      button.setAttribute("aria-label", visible ? "Mostrar contraseña" : "Ocultar contraseña");
      button.setAttribute("aria-pressed", String(!visible));
    });
    input.addEventListener("input", () => { if (input.type === "text") input.type = "password"; }, { once: true });
  });
}
installPasswordToggles();
function show(step, message) {
  for (const id of ["login", "verify", "change", "recover", "reset"]) el(id).hidden = id !== step;
  el("forgot").hidden = step !== "login";
  el("cancel").hidden = step === "login";
  setMessage(message, message ? "info" : "");
  if (step === "reset") scheduleRecoveryResend();
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
  try { await operation(); } catch (error) { setMessage(error.message, "error"); }
  finally {
    busy = false;
    document.querySelectorAll("button").forEach(b => b.disabled = false);
    if (!el("reset").hidden) scheduleRecoveryResend();
  }
}
el("login").addEventListener("submit", e => { e.preventDefault(); run(async () => {
  if (location.protocol !== "https:") throw new Error("Abra ARGOS mediante HTTPS para iniciar sesión.");
  try { challenge = await post("begin", {identification:el("identification").value, password:el("password").value}); }
  finally { el("password").value = ""; }
  show("verify", "Revise el correo enviado a " + challenge.maskedDestination + " y siga los cuatro pasos indicados abajo."); el("code").focus();
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
  if (el("new").value === el("current").value) throw new Error("La nueva contraseña debe ser diferente de la contraseña actual.");
  try { await post("password/change", {currentPassword:el("current").value, newPassword:el("new").value}); }
  finally { for (const id of ["current","new","repeat"]) el(id).value = ""; }
  show("login", "Contraseña cambiada. Ingrese de nuevo con la nueva contraseña.");
}); });
el("cancel").addEventListener("click", () => run(async () => {
  if (recovery) await post("recovery/cancel", {id:recovery.id, binding:recovery.binding});
  recovery = null;
  recoveryResendAt = 0;
  if (recoveryResendTimer) { clearInterval(recoveryResendTimer); recoveryResendTimer = null; }
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
  recoveryResendAt = Date.now() + 60_000;
  show("reset", "Si la identificación corresponde a una cuenta habilitada con correo verificado, recibirá un código. Revise también el correo no deseado.");
  el("recovery-code").focus();
}); });
el("resend-recovery").addEventListener("click", () => run(async () => {
  if (!recovery || Date.now() < recoveryResendAt) return;
  recovery = await post("recovery/begin", {identification:el("recovery-identification").value});
  recoveryResendAt = Date.now() + 60_000;
  for (const id of ["recovery-code", "recovery-password", "recovery-repeat"]) el(id).value = "";
  show("reset", "Solicitamos un código nuevo. El código anterior ya no es válido.");
  el("recovery-code").focus();
}));
el("reset").addEventListener("submit", e => { e.preventDefault(); run(async () => {
  if (el("recovery-password").value !== el("recovery-repeat").value) throw new Error("Las contraseñas nuevas no coinciden.");
  try { await post("recovery/complete", {id:recovery.id,binding:recovery.binding,code:el("recovery-code").value,newPassword:el("recovery-password").value}); }
  finally { for (const id of ["recovery-code","recovery-password","recovery-repeat"]) el(id).value = ""; }
  recovery = null;
  show("login", "Contraseña restablecida. Las sesiones anteriores quedaron invalidadas. Ingrese con su identificación y la nueva contraseña.");
}); });
