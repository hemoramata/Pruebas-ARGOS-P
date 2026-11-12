"use strict";
const el=id=>document.getElementById(id);
let loading=false;
function clear(){el("workspace").hidden=true;el("organization").hidden=true;el("personnel").hidden=true;el("access").hidden=true;el("documentCategories").hidden=true;el("procedures").hidden=true;el("audit").hidden=true;el("logout").hidden=true;el("welcome").textContent="Bienvenido a ARGOS";}
async function refresh(){
  if(loading)return;loading=true;el("refresh").disabled=true;
  try{
    const response=await fetch("/api/session/profile",{cache:"no-store",credentials:"same-origin"});
    if(response.status===401||response.status===403){clear();location.replace("/login/?v=home-20260922");return;}
    if(!response.ok)throw Error("No se pudieron comprobar sus permisos. Intente actualizar nuevamente.");
    const profile=await response.json();
    if(profile.mustChangePassword){clear();location.replace("/login/?v=home-20260922");return;}
    const permissions=new Set(profile.permissions||[]);
    el("welcome").textContent="Bienvenido, "+(profile.fullName||"usuario");
    el("organization").hidden=!permissions.has("personnel.manage");
    el("personnel").hidden=!permissions.has("personnel.manage");
    el("access").hidden=!permissions.has("personnel.manage");
    el("documentCategories").hidden=!permissions.has("personnel.manage");
    el("procedures").hidden=!permissions.has("procedures.manage");
    el("audit").hidden=!permissions.has("personnel.manage");
    el("empty").hidden=permissions.has("personnel.manage")||permissions.has("procedures.manage");
    el("workspace").hidden=false;el("logout").hidden=false;el("login").hidden=true;el("message").textContent="Sesión verificada.";
  }catch(error){clear();el("login").hidden=false;el("message").textContent=error.message;}
  finally{loading=false;el("refresh").disabled=false;}
}
el("refresh").addEventListener("click",refresh);
el("logout").addEventListener("click",async()=>{
  el("logout").disabled=true;
  try{const result=await fetch("/api/session/logout",{method:"POST",credentials:"same-origin"});if(!result.ok)throw Error("No se pudo cerrar la sesión. Inténtelo nuevamente.");clear();location.replace("/login/?v=home-20260922");}
  catch(error){el("message").textContent=error.message;}
  finally{el("logout").disabled=false;}
});
window.addEventListener("pageshow",refresh);
setInterval(refresh,60000);
refresh();
