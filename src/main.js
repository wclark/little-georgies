import {
  HAPPY_RATE_WINDOW_DAYS,
  ROLE_INFO,
  advanceDay,
  countStatus,
  createInitialState,
  getAppleYield,
  getEnding,
  getGrowthHappyRate,
  getGrowthReadiness,
  getHappyRate,
  getNextGrowthTarget,
  getRoleMoodCounts,
  getSpecialistReadiness,
  isSeasonOver,
  setChiefPolicy,
  setPlan,
  setRolePlan
} from "./game.js";

const statusLabel = {
  happy: "Happy",
  tired: "Tired",
  broken: "Broken"
};

const debugMode = new URLSearchParams(window.location.search).has("debug");

let state = createInitialState();
let viewPath = { level: "root" };

const screens = [...document.querySelectorAll("[data-screen]")];
const bindings = [...document.querySelectorAll("[data-bind]")].reduce((map, element) => {
  const key = element.dataset.bind;
  map[key] = map[key] ?? [];
  map[key].push(element);
  return map;
}, {});

const actionsEl = document.querySelector("[data-role='actions']");
const featureEl = document.querySelector("[data-role='feature']");
const orchardCopyEl = document.querySelector(".orchard-copy");
const georgieListEl = document.querySelector("[data-role='georgie-list']");
const georgiesEl = document.querySelector("[data-role='georgies']");
const logEl = document.querySelector("[data-role='log']");
const statsEl = document.querySelector("[data-role='stats']");
const conditionStrip = document.querySelector("[data-role='condition-strip']");
const progressPanel = document.querySelector("[data-role='progress-panel']");
const progressLabel = document.querySelector("[data-role='progress-label']");
const progressValue = document.querySelector("[data-role='progress-value']");
const progressMeter = document.querySelector("[data-role='progress-meter']");
const debugPanel = document.querySelector("[data-role='debug-panel']");
const debugOutput = document.querySelector("[data-role='debug-output']");

document.addEventListener("click", (event) => {
  const target = event.target.closest("[data-action]");
  if (!target) return;

  const action = target.dataset.action;
  if (action === "start") {
    viewPath = { level: "root" };
    showScreen("game");
    render();
    return;
  }

  if (action === "reset") {
    state = createInitialState();
    viewPath = { level: "root" };
    showScreen("splash");
    render();
    return;
  }

  if (action === "end-day") {
    const previousPhase = state.phase;
    state = advanceDay(state);
    if (previousPhase !== state.phase) {
      viewPath = { level: "root" };
    }
    syncViewPath();
    finishOrRender();
    if (previousPhase !== state.phase) {
      scrollGameToTop();
    }
    return;
  }

  if (action.startsWith("view:")) {
    viewPath = getNavigationPath(action);
    syncViewPath();
    render();
    scrollGameToTop();
    return;
  }

  if (action.startsWith("plan:")) {
    const [, id, plan] = action.split(":");
    state = setPlan(state, Number(id), plan);
    render();
    return;
  }

  if (action.startsWith("role-plan:")) {
    const [, role, plan] = action.split(":");
    state = setRolePlan(state, role, plan);
    render();
    return;
  }

  if (action.startsWith("chief-policy:")) {
    const [, category, resource, nextValue] = action.split(":");
    state = setChiefPolicy(state, category, resource, nextValue === "on");
    render();
  }
});

render();

function getNavigationPath(action) {
  const [, level, role, id] = action.split(":");

  if (level === "role") {
    return { level: "role", role };
  }

  if (level === "person") {
    return { level: "person", role, id: Number(id) };
  }

  return { level: "root" };
}

function syncViewPath() {
  if (state.phase !== "village") {
    viewPath = { level: "root" };
    return;
  }

  if (viewPath.level === "role" && getRoleGeorgies(viewPath.role).length === 0) {
    viewPath = { level: "root" };
    return;
  }

  if (viewPath.level === "person") {
    const georgie = state.georgies.find((candidate) => candidate.id === viewPath.id && candidate.role === viewPath.role);
    if (!georgie) {
      viewPath = getRoleGeorgies(viewPath.role).length > 0 ? { level: "role", role: viewPath.role } : { level: "root" };
    }
  }
}

function scrollGameToTop() {
  window.scrollTo({ top: 0, left: 0 });
}

function showScreen(name) {
  for (const screen of screens) {
    screen.hidden = screen.dataset.screen !== name;
  }
}

function finishOrRender() {
  if (isSeasonOver(state)) {
    renderEnding();
    showScreen("ending");
    return;
  }

  render();
}

function render() {
  setText("day", state.day);
  setText("phase", getPhaseLabel());
  setText("eventTitle", state.lastEvent.title);
  setText("eventBody", state.lastEvent.body);

  renderFeature();
  renderStageCopy();
  renderStats();
  renderProgress();
  renderConditionStrip();
  renderActions();
  renderGeorgies();
  renderLog();
  renderDebug();
}

function renderStageCopy() {
  orchardCopyEl.hidden = state.phase !== "band";
}

function renderFeature() {
  featureEl.className = `stage-feature ${state.phase}-feature`;

  if (state.phase === "solo") {
    renderSoloFeature();
    return;
  }

  if (state.phase === "village") {
    renderVillageFeature();
    return;
  }

  renderBandFeature();
}

function renderSoloFeature() {
  const georgie = state.georgies[0];
  const role = ROLE_INFO.little;
  featureEl.innerHTML = `
    <article class="solo-card ${georgie.status}">
      <div class="solo-image-wrap">
        <img src="${getGeorgieImage(georgie, "scene")}" alt="${statusLabel[georgie.status]} Little Georgie">
      </div>
      <div class="solo-card-copy">
        <span class="mood-badge ${georgie.status}">${statusLabel[georgie.status]}</span>
        <h3>${role.label}</h3>
        <p>${getSoloTutorialText(georgie)}</p>
        <ul class="tutorial-list">
          <li>Work gathers apples before dinner.</li>
          <li>If an apple is available, the Georgie eats it.</li>
          <li>A fed worker becomes tired; a fed rester becomes happy.</li>
          <li>No apple means the next morning starts broken.</li>
        </ul>
        ${renderGeorgiePlanControl(georgie, role.label)}
      </div>
    </article>
  `;
}

function renderBandFeature() {
  featureEl.innerHTML = `
    <div class="band-grid">
      ${state.georgies.map((georgie) => {
        const role = ROLE_INFO[georgie.role];
        return `
          <article class="person-card ${georgie.status}">
            <img src="${getGeorgieImage(georgie, "scene")}" alt="${statusLabel[georgie.status]} ${georgie.name}">
            <div class="person-copy">
              <span>${role.label}</span>
              <strong>${georgie.name}</strong>
              <small>${statusLabel[georgie.status]} - ${role.workSummary}</small>
            </div>
            ${renderGeorgiePlanControl(georgie, `${georgie.name} plan`)}
          </article>
        `;
      }).join("")}
    </div>
  `;
}

function renderVillageFeature() {
  if (viewPath.level === "person") {
    renderPersonDetailFeature();
    return;
  }

  if (viewPath.level === "role") {
    renderRoleDetailFeature();
    return;
  }

  renderVillageRootFeature();
}

function renderVillageRootFeature() {
  const chief = getRoleGeorgies("chief")[0];
  const farmerCounts = getRoleCounts("farmer");
  const builderCounts = getRoleCounts("builder");

  featureEl.innerHTML = `
    <div class="hierarchy-view">
      ${renderChiefFocus(chief)}
      <div class="tile-grid village-tile-grid" aria-label="Village groups">
        ${renderRoleTile(farmerCounts)}
        ${renderRoleTile(builderCounts)}
      </div>
    </div>
  `;
}

function renderRoleDetailFeature() {
  const counts = getRoleCounts(viewPath.role);
  const georgies = getRoleGeorgies(viewPath.role);
  const role = ROLE_INFO[viewPath.role];

  featureEl.innerHTML = `
    <div class="hierarchy-view">
      ${renderBreadcrumb(`view:root`, "Village")}
      <article class="focus-panel ${counts.median} ${viewPath.role}">
        <img src="${getRoleImage(viewPath.role, counts.median)}" alt="${counts.median} ${role.plural}">
        <div class="focus-copy">
          <span>${role.plural}</span>
          <h3>${role.plural}</h3>
          <p>${role.workSummary}</p>
          ${renderSummaryStats(getRoleStats(viewPath.role))}
          ${renderRolePlanControl(viewPath.role, `${role.plural} plan`)}
        </div>
      </article>
      <div class="tile-grid individual-tile-grid" aria-label="${role.plural}">
        ${georgies.map((georgie) => renderPersonTile(georgie)).join("")}
      </div>
    </div>
  `;
}

function renderPersonDetailFeature() {
  const georgie = state.georgies.find((candidate) => candidate.id === viewPath.id);
  const role = ROLE_INFO[georgie.role];

  featureEl.innerHTML = `
    <div class="hierarchy-view">
      ${renderBreadcrumb(`view:role:${georgie.role}`, role.plural)}
      <article class="focus-panel ${georgie.status} ${georgie.role}">
        <img src="${getRoleImage(georgie.role, georgie.status)}" alt="${statusLabel[georgie.status]} ${formatGeorgieName(georgie)}">
        <div class="focus-copy">
          <span>${role.label}</span>
          <h3>${formatGeorgieName(georgie)}</h3>
          <p>${getPersonSummary(georgie)}</p>
          ${renderSummaryStats(getPersonStats(georgie))}
          ${renderGeorgiePlanControl(georgie, `${formatGeorgieName(georgie)} plan`)}
          ${georgie.role === "chief" ? renderChiefPolicyControls() : ""}
        </div>
      </article>
    </div>
  `;
}

function renderChiefFocus(chief) {
  const role = ROLE_INFO.chief;

  return `
    <article class="focus-panel ${chief.status} chief">
      <img src="${getRoleImage("chief", chief.status)}" alt="${statusLabel[chief.status]} Chief Henry">
      <div class="focus-copy">
        <span>${role.label}</span>
        <h3>Chief Henry</h3>
        <p>${role.workSummary}</p>
        ${renderSummaryStats(getPersonStats(chief))}
        ${renderGeorgiePlanControl(chief, "Chief Henry plan")}
        ${renderChiefPolicyControls()}
      </div>
    </article>
  `;
}

function renderRoleTile(counts) {
  const role = ROLE_INFO[counts.role];

  return `
    <article class="nav-tile ${counts.median} ${counts.role}">
      <button class="tile-open" type="button" data-action="view:role:${counts.role}">
        <img src="${getRoleImage(counts.role, counts.median, "avatar")}" alt="">
        <span>${role.plural}</span>
        <strong>${counts.total} total</strong>
        <small>${counts.happy} happy / ${counts.tired} tired / ${counts.broken} broken</small>
        <small>${getRoleProductionSummary(counts.role)}</small>
      </button>
      ${renderRolePlanControl(counts.role, `${role.plural} plan`)}
    </article>
  `;
}

function renderPersonTile(georgie) {
  const role = ROLE_INFO[georgie.role];

  return `
    <article class="nav-tile ${georgie.status} ${georgie.role}">
      <button class="tile-open" type="button" data-action="view:person:${georgie.role}:${georgie.id}">
        <img src="${getRoleImage(georgie.role, georgie.status, "avatar")}" alt="">
        <span>${role.label}</span>
        <strong>${formatGeorgieName(georgie)}</strong>
        <small>${statusLabel[georgie.status]} - ${getPersonOutput(georgie)}</small>
      </button>
      ${renderGeorgiePlanControl(georgie, `${formatGeorgieName(georgie)} plan`)}
    </article>
  `;
}

function renderBreadcrumb(action, label) {
  return `
    <div class="breadcrumb-row">
      <button type="button" data-action="${action}">Up to ${label}</button>
    </div>
  `;
}

function renderSummaryStats(stats) {
  return `
    <dl class="summary-stats">
      ${stats.map((stat) => `
        <div>
          <dt>${stat.label}</dt>
          <dd>${stat.value}</dd>
        </div>
      `).join("")}
    </dl>
  `;
}

function renderRolePlanControl(role, label) {
  return renderPlanControl({
    activePlan: getRolePlan(role),
    label,
    options: getRolePlanOptions(role).map((plan) => ({
      plan,
      action: `role-plan:${role}:${plan}`,
      label: getPlanLabel(role, plan),
      disabled: isRolePlanDisabled(role, plan)
    }))
  });
}

function renderGeorgiePlanControl(georgie, label) {
  return renderPlanControl({
    activePlan: getGeorgiePlan(georgie),
    label,
    options: getGeorgiePlanOptions(georgie).map((option) => ({
      plan: option.plan,
      action: `plan:${georgie.id}:${option.plan}`,
      label: getPlanLabel(georgie.role, option.plan),
      disabled: option.disabled
    }))
  });
}

function renderPlanControl({ activePlan, label, options }) {
  return `
    <div class="segmented-control option-count-${options.length}" aria-label="${label}">
      ${options.map((option) => `
        <button
          type="button"
          class="${activePlan === option.plan ? "is-active" : ""}"
          data-action="${option.action}"
          ${option.disabled ? "disabled" : ""}
        >${option.label}</button>
      `).join("")}
    </div>
  `;
}

function renderChiefPolicyControls() {
  return `
    <div class="chief-policy" aria-label="Chief Henry resource policy">
      ${renderResourcePolicyRow("levy", "Levy")}
      ${renderResourcePolicyRow("distribute", "Distribute")}
      <p>
        Levy moves selected resources into common stock. Distribute spends common stock before dinner:
        apples refill the food pile, baskets go to farmers, and houses go to unhoused specialists.
      </p>
    </div>
  `;
}

function renderResourcePolicyRow(category, label) {
  const resources = [
    { key: "apples", label: "Apples" },
    { key: "baskets", label: "Baskets" },
    { key: "houses", label: "Houses" }
  ];

  return `
    <div class="resource-policy-row">
      <span>${label}</span>
      <div class="resource-toggle-row">
        ${resources.map((resource) => {
          const enabled = Boolean(state.chiefPolicy?.[category]?.[resource.key]);
          return `
            <button
              type="button"
              class="resource-toggle ${enabled ? "is-active" : ""}"
              data-action="chief-policy:${category}:${resource.key}:${enabled ? "off" : "on"}"
            >${resource.label}</button>
          `;
        }).join("")}
      </div>
    </div>
  `;
}

function renderStats() {
  const stats = [{ label: state.phase === "village" ? "Food apples" : "Apples", value: state.apples }];

  if (state.phase !== "solo") {
    stats.push(
      { label: state.phase === "band" ? "Little Georgies" : "Georgies", value: state.georgies.length },
      { label: `Happy ${HAPPY_RATE_WINDOW_DAYS}-day`, value: `${Math.round(getHappyRate(state) * 100)}%` },
      { label: "Total apples", value: state.totalApples }
    );
  }

  if (state.phase === "village") {
    stats.push(
      { label: "Baskets free", value: state.baskets },
      { label: "Houses free", value: state.houses },
      { label: "Common apples", value: state.commons }
    );
  }

  statsEl.innerHTML = stats.map((stat) => `
    <div class="stat">
      <span>${stat.label}</span>
      <strong>${stat.value}</strong>
    </div>
  `).join("");
}

function renderProgress() {
  if (state.phase !== "band") {
    progressPanel.hidden = true;
    return;
  }

  const value = getSpecialistReadiness(state);
  progressPanel.hidden = false;
  progressLabel.textContent = "Specialist readiness";
  progressValue.textContent = value;
  progressMeter.style.width = `${value}%`;
}

function renderConditionStrip() {
  if (state.phase === "solo") {
    conditionStrip.hidden = true;
    return;
  }

  conditionStrip.hidden = false;
  conditionStrip.innerHTML = `
    <span><strong>${countStatus(state, "happy")}</strong> happy</span>
    <span><strong>${countStatus(state, "tired")}</strong> tired</span>
    <span><strong>${countStatus(state, "broken")}</strong> broken</span>
  `;
}

function renderActions() {
  actionsEl.innerHTML = renderEndDayButton();
}

function renderEndDayButton() {
  return `
    <button class="end-day-button" type="button" data-action="end-day">
      <span>Resolve day</span>
      <strong>${getResolveTitle()}</strong>
      <small>${getResolveSummary()}</small>
    </button>
  `;
}

function renderGeorgies() {
  georgieListEl.hidden = true;
  georgiesEl.innerHTML = "";
}

function renderLog() {
  logEl.innerHTML = state.log.slice(0, 6).map((entry) => `<li>${entry}</li>`).join("");
}

function renderDebug() {
  if (!debugMode) {
    debugPanel.hidden = true;
    return;
  }

  debugPanel.hidden = false;
  debugOutput.textContent = JSON.stringify(getDebugState(), null, 2);
}

function renderEnding() {
  const ending = getEnding(state);
  setText("endingKicker", ending.kicker);
  setText("endingTitle", ending.title);
  setText("endingBody", ending.body);
  setText("endingScore", ending.score);
  setText("endingHappy", ending.happy);
  setText("endingBroken", ending.broken);
}

function setText(key, value) {
  for (const element of bindings[key] ?? []) {
    element.textContent = value;
  }
}

function getPhaseLabel() {
  if (state.phase === "solo") return "First Little Georgie";
  if (state.phase === "band") return "Named Little Georgies";
  return "Specialist village";
}

function getSoloTutorialText(georgie) {
  if (georgie.status === "happy") {
    return "This first stage is intentionally small: one Georgie, one apple pile, and one choice each day.";
  }

  if (georgie.status === "tired") {
    return "A tired Georgie can keep working, but rest plus an apple is what brings happiness back.";
  }

  return "A broken Georgie needs food immediately. Rest only helps if there is an apple to eat.";
}

function getResolveTitle() {
  if (state.phase === "solo") return "Resolve this Georgie's day";
  if (state.phase === "band") return "Feed the named band";
  return "Resolve the village day";
}

function getResolveSummary() {
  if (state.phase === "solo") {
    return "The chosen action happens first. Then the apple pile decides tomorrow's mood.";
  }

  if (state.phase === "band") {
    return "Every Little Georgie follows their plan, then eats if an apple is available.";
  }

  return "Work happens first. Chief Henry moves selected resources, common apples can refill the food pile, then everyone eats if apples are available.";
}

function getRoleGeorgies(role) {
  return state.georgies.filter((georgie) => georgie.role === role).sort((a, b) => a.id - b.id);
}

function getRoleCounts(role) {
  return getRoleMoodCounts(state).find((counts) => counts.role === role);
}

function getRolePlan(role) {
  const plans = new Set(getRoleGeorgies(role).map((georgie) => getGeorgiePlan(georgie)));
  if (plans.size === 1) {
    return [...plans][0];
  }

  return "mixed";
}

function getGeorgiePlan(georgie) {
  if (georgie.status === "broken") return getDefaultWorkPlan(georgie.role);
  const plan = georgie.plan ?? state.rolePlans[georgie.role] ?? getDefaultWorkPlan(georgie.role);
  if (georgie.role === "builder" && plan === "work") return "basket";
  return plan;
}

function getPlanLabel(role, plan) {
  if (plan === "mixed") return "Mixed";
  if (plan === "rest") return ROLE_INFO[role].restLabel;
  if (role === "builder" && plan === "house") return ROLE_INFO.builder.houseLabel;
  return ROLE_INFO[role].workLabel;
}

function getDefaultWorkPlan(role) {
  return role === "builder" ? "basket" : "work";
}

function getRolePlanOptions(role) {
  if (role === "builder") return ["basket", "house", "rest"];
  return ["work", "rest"];
}

function getGeorgiePlanOptions(georgie) {
  return getRolePlanOptions(georgie.role).map((plan) => ({
    plan,
    disabled: isGeorgiePlanDisabled(georgie, plan)
  }));
}

function isRolePlanDisabled(role, plan) {
  return !getRoleGeorgies(role).some((georgie) => !isGeorgiePlanDisabled(georgie, plan));
}

function isGeorgiePlanDisabled(georgie, plan) {
  return georgie.status === "broken" && (plan === "rest" || (georgie.role === "builder" && plan === "house"));
}

function getRoleStats(role) {
  const counts = getRoleCounts(role);
  const georgies = getRoleGeorgies(role);
  return [
    { label: "Total", value: counts.total },
    { label: "Median mood", value: statusLabel[counts.median] },
    { label: "Mood mix", value: `${counts.happy} happy / ${counts.tired} tired / ${counts.broken} broken` },
    { label: "Plan", value: getPlanLabel(role, getRolePlan(role)) },
    { label: "Baskets held", value: georgies.filter((georgie) => georgie.hasBasket).length },
    { label: "Housed", value: georgies.filter((georgie) => georgie.hasHouse).length },
    { label: "Output", value: getRoleProductionSummary(role) }
  ];
}

function getPersonStats(georgie) {
  return [
    { label: "Mood", value: statusLabel[georgie.status] },
    { label: "Plan", value: getPlanLabel(georgie.role, getGeorgiePlan(georgie)) },
    { label: "Basket", value: georgie.hasBasket ? "Held" : "None" },
    { label: "House", value: georgie.hasHouse ? "Housed" : "None" },
    { label: "Output", value: getPersonOutput(georgie) }
  ];
}

function getPersonSummary(georgie) {
  if (georgie.role === "chief") {
    return "Henry can rest when able, or administer the common stock. Levy collects selected resources; distribute sends them back out before dinner.";
  }

  if (georgie.status === "broken") {
    return `${formatGeorgieName(georgie)} is broken and can only do minimal work until fed.`;
  }

  return `${formatGeorgieName(georgie)} keeps an individual plan. A house means waking rested after eating, and a basket only helps the farmer holding it.`;
}

function getRoleProductionSummary(role) {
  const georgies = getRoleGeorgies(role);

  if (role === "farmer") {
    const apples = georgies.reduce((total, georgie) => {
      if (getGeorgiePlan(georgie) === "rest") return total;
      return total + getAppleYield(georgie);
    }, 0);
    return `${apples} apples/day`;
  }

  if (role === "builder") {
    const output = georgies.reduce(
      (total, georgie) => {
        const projection = getBuilderProjection(georgie.status, getGeorgiePlan(georgie));
        return {
          baskets: total.baskets + projection.baskets,
          houseProgress: total.houseProgress + projection.houseProgress
        };
      },
      { baskets: 0, houseProgress: 0 }
    );
    return `${output.baskets} baskets/day, ${output.houseProgress} house progress/day`;
  }

  const levy = georgies.reduce((total, georgie) => total + (georgie.status === "happy" ? 2 : georgie.status === "tired" ? 1 : 0), 0);
  return `${levy} levy capacity/day`;
}

function getPersonOutput(georgie) {
  const plan = getGeorgiePlan(georgie);
  if (plan === "rest") return "resting";

  if (georgie.role === "farmer") {
    return `${getAppleYield(georgie)} apples/day`;
  }

  if (georgie.role === "builder") {
    const output = getBuilderProjection(georgie.status, plan);
    return `${output.baskets} baskets/day, ${output.houseProgress} house progress/day`;
  }

  const levy = georgie.status === "happy" ? 2 : georgie.status === "tired" ? 1 : 0;
  return `${levy} levy capacity/day`;
}

function getBuilderProjection(status, plan) {
  if (plan === "house") {
    if (status === "broken") return { baskets: 0, houseProgress: 0 };
    return { baskets: 0, houseProgress: status === "happy" ? 2 : 1 };
  }

  if (plan === "basket") {
    return { baskets: status === "happy" ? 2 : 1, houseProgress: 0 };
  }

  return { baskets: 0, houseProgress: 0 };
}

function formatGeorgieName(georgie) {
  if (georgie.role === "chief") return `Chief ${georgie.name}`;
  if (georgie.role === "little") return georgie.name;
  return `${ROLE_INFO[georgie.role].label.replace(" Georgie", "")} ${georgie.name}`;
}

function getDebugState() {
  return {
    url: `${window.location.pathname}${window.location.search}`,
    viewPath,
    phase: state.phase,
    day: state.day,
    apples: state.apples,
    totalApples: state.totalApples,
    moodHistoryWindowDays: HAPPY_RATE_WINDOW_DAYS,
    moodHistory: state.moodHistory,
    happyRate: roundPercent(getHappyRate(state)),
    growthHappyRate: roundPercent(getGrowthHappyRate(state)),
    nextGrowthTarget: getNextGrowthTarget(state),
    growthReadiness: getGrowthReadiness(state),
    specialistReadiness: getSpecialistReadiness(state),
    baskets: state.baskets,
    houses: state.houses,
    houseProgress: state.houseProgress,
    commons: state.commons,
    rolePlans: state.rolePlans,
    chiefPolicy: state.chiefPolicy,
    georgies: state.georgies.map((georgie) => ({
      id: georgie.id,
      name: georgie.name,
      role: georgie.role,
      status: georgie.status,
      plan: georgie.plan,
      effectivePlan: getGeorgiePlan(georgie),
      hasBasket: georgie.hasBasket,
      hasHouse: georgie.hasHouse,
      isNew: georgie.isNew
    })),
    log: state.log
  };
}

function roundPercent(value) {
  return `${Math.round(value * 100)}%`;
}

function getGeorgieImage(georgie, mode) {
  if (georgie.role === "little") {
    if (mode === "scene") {
      return `./assets/images/georgie-${georgie.status}-scene.png`;
    }

    return `./assets/images/georgie-${georgie.status}.png`;
  }

  return getRoleImage(georgie.role, georgie.status, mode);
}

function getRoleImage(role, status, mode = "scene") {
  const suffix = mode === "avatar" ? "-avatar" : "";
  return `./assets/images/${role}-${status}${suffix}.png`;
}
