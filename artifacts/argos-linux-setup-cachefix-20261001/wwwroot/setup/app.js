"use strict";

const concepts = [
  ["Organization", "Organización"],
  ["OrganizationUnit", "Unidad organizacional"],
  ["InstitutionalPosition", "Posición institucional"],
  ["Person", "Persona"],
  ["Assignment", "Asignación"],
  ["Competence", "Competencia"],
  ["ActingCapacity", "Calidad de actuación"]
];

const state = { setup: null, step: 1, busy: false };
const termExamples = {
  Organization: ["Cómo llaman al tipo de institución o empresa.", "Ministerio", "Ministerios"],
  OrganizationUnit: ["Cómo llaman a las divisiones de su institución.", "Departamento", "Departamentos"],
  InstitutionalPosition: ["Cómo llaman a un puesto de trabajo, independientemente de quién lo ocupe.", "Puesto", "Puestos"],
  Person: ["Cómo llaman a las personas que trabajan en la institución.", "Funcionario", "Funcionarios"],
  Assignment: ["Cómo llaman al vínculo de una persona con el puesto que ocupa.", "Nombramiento", "Nombramientos"],
  Competence: ["Cómo llaman a las funciones o atribuciones que corresponden a un puesto.", "Atribución", "Atribuciones"],
  ActingCapacity: ["Cómo llaman a la condición en que alguien ejerce un puesto, por ejemplo, titular o suplente.", "Calidad de actuación", "Calidades de actuación"]
};
const byId = id => document.getElementById(id);

document.addEventListener("DOMContentLoaded", async () => {
  buildTermRows();
  bindEvents();
  await recoverCurrentSetup();
  showStep(state.step);
});

function bindEvents() {
  byId("organizationForm").addEventListener("submit", createOrganization);
  byId("termsForm").addEventListener("submit", saveTerms);
  byId("unitForm").addEventListener("submit", addUnit);
  byId("unitList").addEventListener("click", handleUnitAction);
  byId("competenceForm").addEventListener("submit", addCompetence);
  byId("capacityForm").addEventListener("submit", addCapacity);
  byId("positionForm").addEventListener("submit", addPosition);
  byId("positionUnit").addEventListener("change", suggestPositionCode);
  byId("positionName").addEventListener("input", suggestPositionCode);
  byId("unitsContinue").addEventListener("click", () => requireCollection("units", 4));
  byId("catalogsContinue").addEventListener("click", continueFromCatalogs);
  byId("positionsContinue").addEventListener("click", () => requireCollection("positions", 6));
  byId("publishButton").addEventListener("click", publishSetup);
  byId("newVersionButton").addEventListener("click", createNextVersion);
  byId("initialAdministratorForm").addEventListener("submit", createInitialAdministrator);
  document.querySelectorAll("#progress li").forEach(item => item.addEventListener("click", () => { const step = Number(item.dataset.step); if (canVisitStep(step)) showStep(step); else notify("Complete primero los datos requeridos de los pasos anteriores.", "error"); }));
  document.querySelectorAll("[data-back]").forEach(button =>
    button.addEventListener("click", () => showStep(Number(button.dataset.back))));
}

async function recoverCurrentSetup() {
  try {
    const response = await fetch("/api/organization-setups/current");
    if (response.status === 204) {
      setConnection("Sistema disponible", true);
      return;
    }
    state.setup = await readResponse(response);
    fillOrganization();
    state.step = suggestedStep();
    renderAll();
    setConnection(`Versión ${state.setup.version} · ${translateStatus(state.setup.status)}`, true);
    notify("Recuperamos la configuración guardada en este equipo.", "success");
  } catch (error) {
    setConnection("No se pudo conectar", false);
    notify(error.message, "error");
  }
}

async function createOrganization(event) {
  event.preventDefault();
  if (state.setup) {
    showStep(2);
    return;
  }

  await runBusy(async () => {
    state.setup = await api("/api/organization-setups/", "POST", {
      organizationName: byId("organizationName").value,
      organizationCode: byId("organizationCode").value,
      description: byId("organizationDescription").value,
      action: actionData()
    });
    fillOrganization();
    renderAll();
    notify("Institución creada. Ahora defina su vocabulario oficial.", "success");
    showStep(2);
  });
}

async function saveTerms(event) {
  event.preventDefault();
  await runBusy(async () => {
    await ensureEditableSetup();
    for (const [concept] of concepts) {
      state.setup = await api(
        `/api/organization-setups/${state.setup.id}/terms/${concept}`,
        "PUT",
        {
          singular: byId(`term-${concept}-singular`).value,
          plural: byId(`term-${concept}-plural`).value
        });
    }
    renderAll();
    notify("Vocabulario guardado.", "success");
    showStep(3);
  });
}

async function addUnit(event) {
  event.preventDefault();
  await runBusy(async () => {
    const editingId = event.target.dataset.editingId;
    await ensureEditableSetup();
    if (editingId) {
      state.setup = await api(`/api/organization-setups/${state.setup.id}/units/${editingId}`, "PUT", {
        name: byId("unitName").value
      });
      delete event.target.dataset.editingId;
      byId("unitCode").disabled = false;
      byId("unitSubmit").textContent = "Agregar unidad";
      event.target.reset();
      renderAll();
      notify("Unidad modificada.", "success");
      return;
    }
    state.setup = await api(`/api/organization-setups/${state.setup.id}/units`, "POST", {
      code: byId("unitCode").value,
      name: byId("unitName").value,
      parentUnitId: byId("unitParent").value || null
    });
    event.target.reset();
    renderAll();
    notify("Unidad agregada.", "success");
  });
}

async function handleUnitAction(event) {
  const button = event.target.closest("button[data-unit-action]");
  if (!button) return;
  const unit = state.setup.units.find(item => item.id === button.dataset.unitId);
  if (!unit) return;
  if (button.dataset.unitAction === "edit") {
    byId("unitCode").value = unit.code;
    byId("unitCode").disabled = true;
    byId("unitName").value = unit.name;
    byId("unitParent").value = unit.parentUnitId || "";
    byId("unitForm").dataset.editingId = unit.id;
    byId("unitSubmit").textContent = "Guardar cambios";
    return;
  }
  if (!confirm(`¿Retirar la unidad “${unit.name}”? Solo se puede retirar si no tiene dependencias.`)) return;
  await runBusy(async () => {
    await ensureEditableSetup();
    state.setup = await api(`/api/organization-setups/${state.setup.id}/units/${unit.id}`, "DELETE");
    renderAll();
    notify("Unidad retirada de esta versión de configuración.", "success");
  });
}

async function addCompetence(event) {
  event.preventDefault();
  await addCatalog(event, "competences", {
    code: byId("competenceCode").value,
    name: byId("competenceName").value,
    description: byId("competenceDescription").value
  }, "Competencia agregada.");
}

async function addCapacity(event) {
  event.preventDefault();
  await addCatalog(event, "acting-capacities", {
    code: byId("capacityCode").value,
    name: byId("capacityName").value,
    description: byId("capacityDescription").value
  }, "Calidad de actuación agregada.");
}

async function addCatalog(event, route, body, message) {
  await runBusy(async () => {
    await ensureEditableSetup();
    state.setup = await api("/api/organization-setups/" + state.setup.id + "/" + route, "POST", body);
    event.target.reset();
    renderAll();
    notify(message, "success");
  });
}

async function addPosition(event) {
  event.preventDefault();
  await runBusy(async () => {
    const code = byId("positionCode").value.trim().toUpperCase();
    await ensureEditableSetup();
    state.setup = await api(`/api/organization-setups/${state.setup.id}/positions`, "POST", {
      code,
      name: byId("positionName").value,
      organizationUnitId: byId("positionUnit").value
    });
    const position = state.setup.positions.find(item => item.code === code);
    state.setup = await api(
      `/api/organization-setups/${state.setup.id}/positions/${position.id}/competences/${byId("positionCompetence").value}`,
      "POST");
    state.setup = await api(
      `/api/organization-setups/${state.setup.id}/positions/${position.id}/acting-capacities/${byId("positionCapacity").value}`,
      "POST");
    event.target.reset();
    renderAll();
    notify("Puesto agregado con su competencia y calidad iniciales.", "success");
  });
}

async function publishSetup() {
  if (state.setup.status === "Published") return;
  await runBusy(async () => {
    state.setup = await api(
      `/api/organization-setups/${state.setup.id}/publish`,
      "POST",
      actionData());
    renderAll();
    setConnection(`Versión ${state.setup.version} · Publicada`, true);
    notify("Configuración publicada correctamente.", "success");
  });
}

async function createNextVersion() {
  await runBusy(async () => {
    state.setup = await api(`/api/organization-setups/${state.setup.id}/versions`, "POST", actionData());
    renderAll();
    setConnection(`Versión ${state.setup.version} · Borrador`, true);
    notify("Se creó una nueva versión editable. La versión publicada permanece intacta.", "success");
    showStep(2);
  });
}

async function createInitialAdministrator(event) {
  event.preventDefault();
  const feedback = byId("initialAdministratorMessage");
  const submit = event.target.querySelector("button[type=submit]");
  const showFeedback = (message, error = false) => {
    feedback.textContent = message;
    feedback.className = `form-message span-2${error ? " error" : ""}`;
    feedback.hidden = false;
  };
  const password = byId("initialAdminPassword").value;
  if (password !== byId("initialAdminRepeat").value) {
    showFeedback("Las contraseñas no coinciden. Corríjalas antes de continuar.", true);
    return;
  }
  showFeedback("Creando la cuenta inicial…");
  submit.disabled = true;
  await runBusy(async () => {
    try {
      await api("/api/initial-administrator", "POST", {
        fullName: byId("initialAdminName").value,
        identification: byId("initialAdminIdentification").value,
        email: byId("initialAdminEmail").value,
        password
      });
      showFeedback("Cuenta creada. Redirigiendo al acceso…");
      event.target.reset();
      window.location.assign("/login/");
    } catch (error) {
      const detail = error.message === "No fue posible completar la operación."
        ? "No se pudo crear la cuenta. Verifique que la identificación no exista y vuelva a intentarlo."
        : error.message;
      showFeedback(detail, true);
      notify(detail, "error");
    }
  });
  submit.disabled = false;
}

async function ensureEditableSetup() {
  if (!state.setup || state.setup.status === "Draft") return;
  state.setup = await api(
    "/api/organization-setups/" + state.setup.id + "/versions",
    "POST",
    actionData());
  renderAll();
  setConnection("Versión " + state.setup.version + " · Borrador", true);
  notify("Se creó una nueva versión editable para conservar intacta la versión publicada.", "success");
}

function actionData() {
  return {
    actorReference: byId("actorReference").value,
    policyVersion: byId("policyVersion").value,
    authorizationReference: byId("authorizationReference").value
  };
}

async function api(url, method, body) {
  const options = { method, headers: {} };
  if (body !== undefined) {
    options.headers["Content-Type"] = "application/json";
    options.body = JSON.stringify(body);
  }
  return readResponse(await fetch(url, options));
}

async function readResponse(response) {
  if (response.status === 401) {
    location.assign("/login/");
    throw new Error("Inicie sesión con una cuenta institucional autorizada.");
  }
  if (response.status === 403) throw new Error("Su sesión no tiene permiso para administrar esta institución. Ingrese con una cuenta autorizada en /login/.");
  if (response.ok) return response.status === 204 ? null : response.json();
  let payload = null;
  try { payload = await response.json(); } catch { /* respuesta sin JSON */ }
  const validation = payload?.errors ? Object.values(payload.errors).flat()[0] : null;
  throw new Error(validation || payload?.description || "No fue posible completar la operación.");
}

async function runBusy(operation) {
  if (state.busy) return;
  state.busy = true;
  document.querySelectorAll("button").forEach(button => button.disabled = true);
  try {
    await operation();
  } catch (error) {
    notify(error.message, "error");
  } finally {
    state.busy = false;
    document.querySelectorAll("button").forEach(button => button.disabled = false);
  }
}

function showStep(step) {
  let targetStep = Number(step);
  if (!Number.isInteger(targetStep) || targetStep < 1 || targetStep > 6) return;
  if (targetStep > 1 && !state.setup) {
    notify("Primero debe identificar la institución.", "error");
    state.step = 1;
    targetStep = 1;
  }
  state.step = targetStep;

  // Rehidrata las listas y desplegables al volver atrás y avanzar otra vez.
  // Los formularios que aún no se han guardado conservan sus valores.
  if (targetStep >= 3) renderUnits();
  if (targetStep >= 4) renderCatalogs();
  if (targetStep >= 5) renderPositions();
  if (targetStep === 6) renderReview();

  document.querySelectorAll(".step").forEach(section =>
    section.hidden = Number(section.dataset.step) !== targetStep);
  document.querySelectorAll("#progress li").forEach(item => {
    const itemStep = Number(item.dataset.step);
    item.classList.toggle("active", itemStep === targetStep);
    item.classList.toggle("complete", itemStep < targetStep || completionFor(itemStep));
    item.classList.toggle("clickable", canVisitStep(itemStep));
  });
  window.scrollTo({ top: 0, behavior: "smooth" });
}

function suggestedStep() {
  if (state.setup.status === "Published") return 6;
  if (state.setup.terms.length < concepts.length) return 2;
  if (!state.setup.units.length) return 3;
  if (!state.setup.competences.length || !state.setup.actingCapacities.length) return 4;
  if (!state.setup.positions.length) return 5;
  return 6;
}

function canVisitStep(step) {
  if (!state.setup) return step === 1;
  if (state.setup.status === "Published") return step >= 1 && step <= 6;
  return step === 1
    || step === 2
    || (step === 3 && state.setup.terms.length === concepts.length)
    || (step === 4 && state.setup.units.length > 0)
    || (step === 5 && state.setup.competences.length > 0 && state.setup.actingCapacities.length > 0)
    || (step === 6 && state.setup.positions.length > 0);
}

function completionFor(step) {
  if (!state.setup) return false;
  return [
    false,
    true,
    state.setup.terms.length === concepts.length,
    state.setup.units.length > 0,
    state.setup.competences.length > 0 && state.setup.actingCapacities.length > 0,
    state.setup.positions.length > 0,
    state.setup.status === "Published"
  ][step];
}

function requireCollection(property, nextStep) {
  if (!state.setup[property].length) {
    notify("Agregue al menos un elemento antes de continuar.", "error");
    return;
  }
  showStep(nextStep);
}

function continueFromCatalogs() {
  if (!state.setup.competences.length || !state.setup.actingCapacities.length) {
    notify("Agregue al menos una competencia y una calidad de actuación.", "error");
    return;
  }
  showStep(5);
}

function buildTermRows() {
  const container = byId("termRows");
  concepts.forEach(([concept, label]) => {
    const row = document.createElement("div");
    row.className = "term-row";
    const heading = document.createElement("div");
    heading.className = "term-concept";
    const strong = document.createElement("strong");
    strong.textContent = label;
    const small = document.createElement("small");
    const [description, singular, plural] = termExamples[concept];
    small.textContent = description;
    const example = document.createElement("small");
    example.textContent = `Ejemplo: ${singular} — ${plural}.`;
    heading.append(strong, small);
    heading.append(example);
    row.append(heading, termInput(concept, "singular", "Singular"), termInput(concept, "plural", "Plural"));
    container.append(row);
  });
}

function termInput(concept, kind, labelText) {
  const label = document.createElement("label");
  label.className = "field";
  label.append(document.createTextNode(labelText));
  const input = document.createElement("input");
  input.id = `term-${concept}-${kind}`;
  input.maxLength = 200;
  input.required = true;
  input.placeholder = `Ej.: ${termExamples[concept][kind === "singular" ? 1 : 2]}`;
  label.append(input);
  return label;
}

function fillOrganization() {
  if (!state.setup) return;
  byId("organizationName").value = state.setup.organization.name;
  byId("organizationCode").value = state.setup.organization.code;
  byId("organizationDescription").value = state.setup.organization.description || "";
  ["organizationName", "organizationCode", "organizationDescription"].forEach(id => byId(id).disabled = true);
  byId("organizationForm").querySelector("button").textContent = "Continuar";
  if (!byId("actorReference").value) byId("actorReference").value = state.setup.createdByReference;
  if (!byId("policyVersion").value) byId("policyVersion").value = state.setup.creationPolicyVersion;
  if (!byId("authorizationReference").value) byId("authorizationReference").value = state.setup.creationAuthorizationReference;
}

function renderAll() {
  if (!state.setup) return;
  renderTerms();
  renderUnits();
  renderCatalogs();
  renderPositions();
  renderReview();
}

function renderTerms() {
  state.setup.terms.forEach(term => {
    byId(`term-${term.concept}-singular`).value = term.singular;
    byId(`term-${term.concept}-plural`).value = term.plural;
  });
}

function renderUnits() {
  renderList("unitList", state.setup.units, item => [item.name, item.code], { actions: true });
  fillSelect("unitParent", state.setup.units, "Unidad principal", false);
  fillSelect("positionUnit", state.setup.units, "Seleccione una unidad", true);
  suggestPositionCode();
}

function suggestPositionCode() {
  const code = byId("positionCode");
  const unit = state.setup?.units.find(item => item.id === byId("positionUnit").value);
  const name = byId("positionName").value.trim();
  if (!code.readOnly) return;
  if (!unit || !name) { code.value = ""; return; }
  const slug = name.normalize("NFD").replace(/[\u0300-\u036f]/g, "")
    .toUpperCase().replace(/[^A-Z0-9]+/g, "-").replace(/^-|-$/g, "");
  const base = `${unit.code}-${slug}`.slice(0, 50).replace(/-+$/g, "");
  const existing = new Set(state.setup.positions.map(item => item.code.toUpperCase()));
  let candidate = base;
  let sequence = 2;
  while (existing.has(candidate.toUpperCase())) {
    const suffix = `-${sequence++}`;
    candidate = base.slice(0, 50 - suffix.length).replace(/-+$/g, "") + suffix;
  }
  code.value = candidate;
}

function renderCatalogs() {
  renderList("competenceList", state.setup.competences, item => [item.name, item.code]);
  renderList("capacityList", state.setup.actingCapacities, item => [item.name, item.code]);
  fillSelect("positionCompetence", state.setup.competences, "Seleccione una competencia", true);
  fillSelect("positionCapacity", state.setup.actingCapacities, "Seleccione una calidad", true);
}

function renderPositions() {
  renderList("positionList", state.setup.positions, item => {
    const unit = state.setup.units.find(unitItem => unitItem.id === item.organizationUnitId);
    return [item.name, `${item.code} · ${unit?.name || "Unidad no encontrada"}`];
  });
}

function renderList(id, items, textFactory, options = {}) {
  const list = byId(id);
  list.replaceChildren();
  if (!items.length) {
    const empty = document.createElement("li");
    empty.className = "empty";
    empty.textContent = "Todavía no hay registros";
    list.append(empty);
    return;
  }
  items.forEach(item => {
    const [name, detail] = textFactory(item);
    const li = document.createElement("li");
    const text = document.createElement("div");
    const strong = document.createElement("strong");
    strong.textContent = name;
    const small = document.createElement("small");
    small.textContent = detail;
    text.append(strong, small);
    const code = document.createElement("span");
    code.className = "code";
    code.textContent = "Registrado";
    if (options.actions) {
      const actions = document.createElement("span");
      actions.className = "item-actions";
      for (const [label, action] of [["Modificar", "edit"], ["Retirar", "delete"]]) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = action === "delete" ? "link-button danger" : "link-button";
        button.textContent = label;
        button.dataset.unitAction = action;
        button.dataset.unitId = item.id;
        actions.append(button);
      }
      li.append(text, actions);
    } else li.append(text, code);
    list.append(li);
  });
}

function fillSelect(id, items, placeholder, required) {
  const select = byId(id);
  const current = select.value;
  select.replaceChildren();
  const first = document.createElement("option");
  first.value = "";
  first.textContent = placeholder;
  first.disabled = required;
  first.selected = true;
  select.append(first);
  items.forEach(item => {
    const option = document.createElement("option");
    option.value = item.id;
    option.textContent = `${item.code} — ${item.name}`;
    select.append(option);
  });
  if ([...select.options].some(option => option.value === current)) select.value = current;
}

function renderReview() {
  if (!state.setup) return;
  const cards = [
    [state.setup.terms.length, "términos"],
    [state.setup.units.length, "unidades"],
    [state.setup.positions.length, "puestos"],
    [state.setup.competences.length, "competencias"],
    [state.setup.actingCapacities.length, "calidades"]
  ];
  const container = byId("reviewCards");
  container.replaceChildren();
  cards.forEach(([count, label]) => {
    const card = document.createElement("div");
    card.className = "review-card";
    const strong = document.createElement("strong");
    strong.textContent = count;
    const span = document.createElement("span");
    span.textContent = label;
    card.append(strong, span);
    container.append(card);
  });
  const published = state.setup.status === "Published";
  byId("publishedMessage").hidden = !published;
  byId("publishButton").hidden = published;
  byId("newVersionButton").hidden = !published;
  byId("initialAdministrator").hidden = !published;
}

function notify(message, type = "") {
  const notice = byId("notice");
  notice.textContent = message;
  notice.className = `notice ${type}`.trim();
  notice.hidden = false;
}

function setConnection(message, ready) {
  const pill = byId("connectionStatus");
  pill.textContent = message;
  pill.classList.toggle("ready", ready);
}

function translateStatus(status) {
  return ({ Draft: "Borrador", Published: "Publicada", Retired: "Retirada" })[status] || status;
}
