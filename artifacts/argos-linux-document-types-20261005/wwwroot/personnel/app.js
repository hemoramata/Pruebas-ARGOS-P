"use strict";
const el=id=>document.getElementById(id), message=el("message");
let people=[], positions=[], selectedPersonId="";
const today=()=>new Date().toISOString().slice(0,10);
el("startsAt").value=today();
function tell(text,error=false){message.textContent=text;message.style.color=error?"#b42318":"";}
async function api(url,options={}){const response=await fetch(url,{credentials:"same-origin",cache:"no-store",...options,headers:{"Content-Type":"application/json",...(options.headers||{})}});if(response.status===401||response.status===403){location.replace("/login/");throw Error("Su sesión no tiene autorización para administrar personas.");}if(!response.ok){let detail="";try{detail=(await response.json()).code||"";}catch{}throw Error(detail||"No se pudo completar la operación.");}return response.status===204?null:response.json();}
function renderPeople(){const target=el("people"), select=el("person"), selected=selectedPersonId||select.value;target.replaceChildren();select.innerHTML='<option value="">Seleccione una persona</option>';for(const person of people){const option=document.createElement("option");option.value=person.id;option.textContent=person.fullName+" · "+person.number;select.append(option);const row=document.createElement("div");row.className="person";row.tabIndex=0;row.setAttribute("role","button");row.innerHTML=`<span><strong>${escapeHtml(person.fullName)}</strong><small>${escapeHtml(person.documentType)}: ${escapeHtml(person.number)}</small></span><span class="badge">${person.activeAssignments} nombramiento${person.activeAssignments===1?"":"s"} vigente${person.activeAssignments===1?"":"s"}</span>`;const choose=()=>selectPerson(person.id);row.addEventListener("click",choose);row.addEventListener("keydown",event=>{if(event.key==="Enter"||event.key===" "){event.preventDefault();choose();}});target.append(row);}select.value=selected;if(!people.length)target.innerHTML='<p class="empty">Todavía no hay personas registradas.</p>';select.disabled=!people.length;el("assign").disabled=!people.length||!positions.length;}
function escapeHtml(value){const e=document.createElement("span");e.textContent=value;return e.innerHTML;}
function formatDate(value){return value?new Intl.DateTimeFormat("es-CR",{dateStyle:"medium"}).format(new Date(value)):"Sin fecha de finalización";}
function positionName(id){const position=positions.find(item=>item.id===id);return position?`${position.code} · ${position.name}`:"Puesto no disponible en la versión actual";}
async function selectPerson(id){selectedPersonId=id;el("person").value=id;const target=el("assignment-details");const person=people.find(item=>item.id===id);if(!person){target.innerHTML='<p class="empty">Seleccione una persona para consultar sus nombramientos.</p>';return;}el("selected-person").textContent=`Nombramientos de ${person.fullName}.`;target.innerHTML='<p class="empty">Cargando nombramientos…</p>';try{const detail=await api(`/api/personnel/${id}`);target.replaceChildren();const assignments=(detail.assignments||[]).slice().sort((a,b)=>new Date(b.startsAt)-new Date(a.startsAt));if(!assignments.length){target.innerHTML='<p class="empty">Esta persona todavía no tiene nombramientos registrados.</p>';return;}for(const assignment of assignments){const active=!assignment.revokedAt&&(!assignment.endsAt||new Date(assignment.endsAt)>new Date());const row=document.createElement("div");row.className=`person assignment-row${active?"":" revoked"}`;const end=assignment.revokedAt?`Revocado: ${formatDate(assignment.revokedAt)}`:assignment.endsAt?`Finaliza: ${formatDate(assignment.endsAt)}`:"Vigente sin fecha de finalización";row.innerHTML=`<span><strong>${escapeHtml(positionName(assignment.positionId))}</strong><small>Inicio: ${escapeHtml(formatDate(assignment.startsAt))} · ${escapeHtml(end)}</small></span>`;const actions=document.createElement("span");actions.className="assignment-actions";actions.innerHTML=`<span class="badge">${active?"Vigente":"Histórico"}</span>`;if(active){const revoke=document.createElement("button");revoke.type="button";revoke.className="danger";revoke.textContent="Revocar nombramiento";revoke.addEventListener("click",()=>revokeAssignment(id,assignment.id));actions.append(revoke);}row.append(actions);target.append(row);}}catch(error){target.innerHTML='<p class="empty">No se pudieron cargar los nombramientos.</p>';tell(error.message,true);}}
async function revokeAssignment(personId,assignmentId){if(!confirm("¿Desea revocar este nombramiento? Esta operación queda registrada."))return;try{await api(`/api/personnel/${personId}/assignments/${assignmentId}/revoke`,{method:"POST"});tell("Nombramiento revocado correctamente.");await load();await selectPerson(personId);}catch(error){tell(error.message,true);}}
async function load(){try{tell("Actualizando personas y puestos…");const [listed,setup]=await Promise.all([api("/api/personnel/"),api("/api/organization-setups/current")]);people=listed;positions=(setup.positions||[]).slice().sort((a,b)=>(a.code||"").localeCompare(b.code||""));const select=el("position");select.innerHTML='<option value="">Seleccione un puesto</option>';for(const position of positions){const option=document.createElement("option");option.value=position.id;option.textContent=`${position.code} · ${position.name}`;select.append(option);}select.disabled=!positions.length;renderPeople();tell(positions.length?"Información actualizada.":"No hay puestos publicados disponibles para nombrar.",!positions.length);}catch(error){tell(error.message,true);}}
el("person-form").addEventListener("submit",async event=>{event.preventDefault();const form=new FormData(event.currentTarget);try{const created=await api("/api/personnel/",{method:"POST",body:JSON.stringify(Object.fromEntries(form))});tell("Persona registrada correctamente.");event.currentTarget.reset();event.currentTarget.elements.issuer.value="CR";event.currentTarget.elements.documentType.value="Cédula de identidad";await load();await selectPerson(created.id);}catch(error){tell(error.message,true);}});
el("assignment-form").addEventListener("submit", async event => {
  event.preventDefault();
  const personId = el("person").value;
  const startsAt = new Date(el("startsAt").value + "T00:00:00Z").toISOString();
  const ends = el("endsAt").value;
  try {
    await api(`/api/personnel/${personId}/assignments`, {
      method: "POST",
      body: JSON.stringify({
        positionId: el("position").value,
        startsAt,
        endsAt: ends ? new Date(ends + "T00:00:00Z").toISOString() : null
      })
    });
    tell("Nombramiento registrado correctamente.");
    el("endsAt").value = "";
    await load();
    await selectPerson(personId);
  } catch (error) {
    tell(error.message, true);
  }
});
el("reload").addEventListener("click",load);load();
el("person").addEventListener("change",event=>{if(event.target.value)selectPerson(event.target.value);else{selectedPersonId="";el("selected-person").textContent="Seleccione una persona para consultar o revocar sus nombramientos.";el("assignment-details").innerHTML='<p class="empty">Aún no hay una persona seleccionada.</p>';}});
