"use strict";
const el=id=>document.getElementById(id), message=el("message");
let people=[], positions=[];
const today=()=>new Date().toISOString().slice(0,10);
el("startsAt").value=today();
function tell(text,error=false){message.textContent=text;message.style.color=error?"#b42318":"";}
async function api(url,options={}){const response=await fetch(url,{credentials:"same-origin",cache:"no-store",...options,headers:{"Content-Type":"application/json",...(options.headers||{})}});if(response.status===401||response.status===403){location.replace("/login/");throw Error("Su sesión no tiene autorización para administrar personas.");}if(!response.ok){let detail="";try{detail=(await response.json()).code||"";}catch{}throw Error(detail||"No se pudo completar la operación.");}return response.status===204?null:response.json();}
function renderPeople(){const target=el("people"), select=el("person"), selected=select.value;target.replaceChildren();select.innerHTML='<option value="">Seleccione una persona</option>';for(const person of people){const option=document.createElement("option");option.value=person.id;option.textContent=person.fullName+" · "+person.number;select.append(option);const row=document.createElement("div");row.className="person";row.innerHTML=`<span><strong>${escapeHtml(person.fullName)}</strong><small>${escapeHtml(person.documentType)}: ${escapeHtml(person.number)}</small></span><span class="badge">${person.activeAssignments} nombramiento${person.activeAssignments===1?"":"s"} vigente${person.activeAssignments===1?"":"s"}</span>`;row.addEventListener("click",()=>{select.value=person.id;select.focus();});target.append(row);}select.value=selected;if(!people.length)target.innerHTML='<p class="empty">Todavía no hay personas registradas.</p>';select.disabled=!people.length;el("assign").disabled=!people.length||!positions.length;}
function escapeHtml(value){const e=document.createElement("span");e.textContent=value;return e.innerHTML;}
async function load(){try{tell("Actualizando personas y puestos…");const [listed,setup]=await Promise.all([api("/api/personnel/"),api("/api/organization-setups/current")]);people=listed;positions=(setup.positions||[]).slice().sort((a,b)=>(a.code||"").localeCompare(b.code||""));const select=el("position");select.innerHTML='<option value="">Seleccione un puesto</option>';for(const position of positions){const option=document.createElement("option");option.value=position.id;option.textContent=`${position.code} · ${position.name}`;select.append(option);}select.disabled=!positions.length;renderPeople();tell(positions.length?"Información actualizada.":"No hay puestos publicados disponibles para nombrar.",!positions.length);}catch(error){tell(error.message,true);}}
el("person-form").addEventListener("submit",async event=>{event.preventDefault();const form=new FormData(event.currentTarget);try{const created=await api("/api/personnel/",{method:"POST",body:JSON.stringify(Object.fromEntries(form))});tell("Persona registrada correctamente.");event.currentTarget.reset();event.currentTarget.elements.issuer.value="CR";event.currentTarget.elements.documentType.value="Identificación";await load();el("person").value=created.id;}catch(error){tell(error.message,true);}});
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
  } catch (error) {
    tell(error.message, true);
  }
});
el("reload").addEventListener("click",load);load();
