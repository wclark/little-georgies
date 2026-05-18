import {
  MAX_DAY,
  ROLE_INFO,
  advanceDay,
  countStatus,
  createInitialState,
  getEnding,
  getGrowthReadiness,
  getHappyRate,
  getRoleMoodCounts,
  getSpecialistReadiness,
  isSeasonOver,
  setPlan,
  setRolePlan
} from "./game.js";

const statusLabel = {
  happy: "Happy",
  tired: "Tired",
  broken: "Broken"
};

let state = createInitialState();

const screens = [...document.querySelectorAll("[data-screen]")];
const bindings = [...document.querySelectorAll("[data-bind]")].reduce((map, element) => {
  const key = element.dataset.bind;
  map[key] = map[key] ?? [];
  map[key].push(element);
  return map;
}, {});

const actionsEl = document.querySelector("[data-role='actions']");
const georgiesEl = document.querySelector("[data-role='georgies']");
const logEl = document.querySelector("[data-role='log']");
const statsEl = document.querySelector("[data-role='stats']");
const conditionStrip = document.querySelector("[data-role='condition-strip']");
const progressPanel = document.querySelector("[data-role='progress-panel']");
const progressLabel = document.querySelector("[data-role='progress-label']");
const progressValue = document.querySelector("[data-role='progress-value']");
const progressMeter = document.querySelector("[data-role='progress-meter']");

document.addEventListener("click", (event) => {
  const target = event.target.closest("[data-action]");
  if (!target) return;

  const action = target.dataset.action;
  if (action === "start") {
    showScreen("game");
    render();
    return;
  }

  if (action === "reset") {
    state = createInitialState();
    showScreen("splash");
    render();
    return;
  }

  if (action === "end-day") {
    state = advanceDay(state);
    finishOrRender();
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
  }
});

render();

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
  setText("day", Math.min(state.day, MAX_DAY));
  setText("maxDay", MAX_DAY);
  setText("phase", getPhaseLabel());
  setText("eventTitle", state.lastEvent.title);
  setText("eventBody", state.lastEvent.body);

  renderStats();
  renderProgress();
  renderConditionStrip();
  renderActions();
  renderGeorgies();
  renderLog();
}

function renderStats() {
  const stats = [{ label: "Apples", value: state.apples }];

  if (state.phase !== "solo") {
    stats.push(
      { label: state.phase === "band" ? "Little Georgies" : "Georgies", value: state.georgies.length },
      { label: "Happy turns", value: `${Math.round(getHappyRate(state) * 100)}%` },
      { label: "Total apples", value: state.totalApples }
    );
  }

  if (state.phase === "village") {
    stats.push(
      { label: "Baskets", value: state.baskets },
      { label: "Houses", value: state.houses },
      { label: "Common fund", value: state.commons }
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
  if (state.phase === "solo") {
    progressPanel.hidden = true;
    return;
  }

  progressPanel.hidden = false;
  const value = state.phase === "band" ? getSpecialistReadiness(state) : getGrowthReadiness(state);
  progressLabel.textContent = state.phase === "band" ? "Specialist readiness" : "Growth readiness";
  progressValue.textContent = value;
  progressMeter.style.width = `${value}%`;
}

function renderConditionStrip() {
  if (state.phase === "solo") {
    conditionStrip.hidden = true;
    return;
  }

  conditionStrip.hidden = false;

  if (state.phase === "village") {
    conditionStrip.innerHTML = getRoleMoodCounts(state).map((counts) => `
      <span>
        <strong>${counts.total}</strong> ${ROLE_INFO[counts.role].plural}<br>
        ${counts.happy} happy / ${counts.tired} tired / ${counts.broken} broken
      </span>
    `).join("");
    return;
  }

  conditionStrip.innerHTML = `
    <span><strong>${countStatus(state, "happy")}</strong> happy</span>
    <span><strong>${countStatus(state, "tired")}</strong> tired</span>
    <span><strong>${countStatus(state, "broken")}</strong> broken</span>
  `;
}

function renderActions() {
  if (state.phase === "village") {
    renderVillageActions();
    return;
  }

  const plannerCards = state.georgies.map((georgie) => {
    const role = ROLE_INFO[georgie.role];
    const title = state.phase === "solo" ? role.label : georgie.name;
    const subtitle = state.phase === "solo" ? "The first lesson" : role.label;
    return `
      <article class="planner-card ${georgie.status} ${georgie.role}">
        <img src="${getGeorgieImage(georgie, "scene")}" alt="">
        <div class="planner-copy">
          <span>${subtitle}</span>
          <strong>${title}</strong>
          <small>${state.phase === "solo" ? getSoloTutorialText(georgie) : role.workSummary}</small>
        </div>
        <div class="segmented-control" aria-label="${title} plan">
          <button
            type="button"
            class="${georgie.plan === "work" ? "is-active" : ""}"
            data-action="plan:${georgie.id}:work"
          >${role.workLabel}</button>
          <button
            type="button"
            class="${georgie.plan === "rest" ? "is-active" : ""}"
            data-action="plan:${georgie.id}:rest"
          >${role.restLabel}</button>
        </div>
      </article>
    `;
  }).join("");

  actionsEl.innerHTML = `${plannerCards}${renderEndDayButton()}`;
}

function renderVillageActions() {
  const plannerCards = getRoleMoodCounts(state).map((counts) => {
    const role = ROLE_INFO[counts.role];
    const representative = getRepresentativeStatus(counts);
    return `
      <article class="planner-card ${representative} ${counts.role}">
        <img src="./assets/images/${counts.role}-${representative}.png" alt="">
        <div class="planner-copy">
          <span>${role.plural}</span>
          <strong>${counts.total} total</strong>
          <small>${role.workSummary}</small>
        </div>
        <div class="segmented-control" aria-label="${role.plural} plan">
          <button
            type="button"
            class="${state.rolePlans[counts.role] === "work" ? "is-active" : ""}"
            data-action="role-plan:${counts.role}:work"
          >${role.workLabel}</button>
          <button
            type="button"
            class="${state.rolePlans[counts.role] === "rest" ? "is-active" : ""}"
            data-action="role-plan:${counts.role}:rest"
          >${role.restLabel}</button>
        </div>
      </article>
    `;
  }).join("");

  actionsEl.innerHTML = `${plannerCards}${renderEndDayButton()}`;
}

function renderEndDayButton() {
  return `
    <button class="end-day-button" type="button" data-action="end-day">
      <span>Resolve day</span>
      <strong>Eat apples and start tomorrow</strong>
      <small>Workers act first. Then every Georgie eats if an apple is available.</small>
    </button>
  `;
}

function renderGeorgies() {
  if (state.phase === "village") {
    georgiesEl.innerHTML = getRoleMoodCounts(state).map((counts) => {
      const representative = getRepresentativeStatus(counts);
      return `
        <article class="georgie-card ${representative}">
          <img src="./assets/images/${counts.role}-${representative}-avatar.png" alt="">
          <div>
            <strong>${ROLE_INFO[counts.role].plural}</strong>
            <span>${counts.happy} happy / ${counts.tired} tired / ${counts.broken} broken</span>
          </div>
        </article>
      `;
    }).join("");
    return;
  }

  georgiesEl.innerHTML = state.georgies.map((georgie) => `
    <article class="georgie-card ${georgie.status}">
      <img src="${getGeorgieImage(georgie, "avatar")}" alt="">
      <div>
        <strong>${state.phase === "solo" ? ROLE_INFO.little.label : georgie.name}</strong>
        <span>${statusLabel[georgie.status]}</span>
      </div>
    </article>
  `).join("");
}

function renderLog() {
  logEl.innerHTML = state.log.slice(0, 6).map((entry) => `<li>${entry}</li>`).join("");
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
  return "Anonymous specialist village";
}

function getSoloTutorialText(georgie) {
  if (georgie.status === "happy") {
    return "Work gathers apples. If an apple is available afterward, this happy worker will become tired.";
  }

  if (georgie.status === "tired") {
    return "Rest today. If there is an apple to eat afterward, the Little Georgie becomes happy again.";
  }

  return "A broken Little Georgie needs an apple and rest to recover.";
}

function getGeorgieImage(georgie, mode) {
  if (georgie.role === "little") {
    return `./assets/images/georgie-${georgie.status}.png`;
  }

  const suffix = mode === "avatar" ? "-avatar" : "";
  return `./assets/images/${georgie.role}-${georgie.status}${suffix}.png`;
}

function getRepresentativeStatus(counts) {
  if (counts.happy > 0) return "happy";
  if (counts.tired > 0) return "tired";
  return "broken";
}
