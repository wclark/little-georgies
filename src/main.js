import {
  MAX_DAY,
  ROLE_INFO,
  advanceDay,
  countStatus,
  createInitialState,
  getEnding,
  getReadiness,
  getScore,
  isSeasonOver,
  setPlan
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
const readinessMeter = document.querySelector("[data-bind-style='readiness']");

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
  const readiness = getReadiness(state);

  setText("day", Math.min(state.day, MAX_DAY));
  setText("maxDay", MAX_DAY);
  setText("phase", state.phase === "little" ? "Little band" : "Specialist village");
  setText("apples", state.apples);
  setText("population", state.georgies.length);
  setText("baskets", state.baskets);
  setText("houses", state.houses);
  setText("commons", state.commons);
  setText("harmony", state.harmony.toFixed(1));
  setText("readiness", readiness);
  setText("score", getScore(state));
  setText("eventTitle", state.lastEvent.title);
  setText("eventBody", state.lastEvent.body);
  setText("happyCount", countStatus(state, "happy"));
  setText("tiredCount", countStatus(state, "tired"));
  setText("brokenCount", countStatus(state, "broken"));

  readinessMeter.style.width = `${readiness}%`;

  renderActions();
  renderGeorgies();
  renderLog();
}

function renderActions() {
  const plannerCards = state.georgies.map((georgie) => {
    const role = ROLE_INFO[georgie.role];
    const image = getGeorgieImage(georgie, "scene");
    return `
      <article class="planner-card ${georgie.status} ${georgie.role}">
        <img src="${image}" alt="">
        <div class="planner-copy">
          <span>${role.label}</span>
          <strong>${georgie.name}</strong>
          <small>${role.workSummary}</small>
        </div>
        <div class="segmented-control" aria-label="${georgie.name} plan">
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

  actionsEl.innerHTML = `
    ${plannerCards}
    <button class="end-day-button" type="button" data-action="end-day">
      <span>Resolve day</span>
      <strong>Eat apples and start tomorrow</strong>
      <small>Workers gather first. Then every Georgie eats if an apple is available.</small>
    </button>
  `;
}

function renderGeorgies() {
  georgiesEl.innerHTML = state.georgies.map((georgie) => `
    <article class="georgie-card ${georgie.status}">
      <img src="${getGeorgieImage(georgie, "avatar")}" alt="">
      <div>
        <strong>${georgie.name}</strong>
        <span>${ROLE_INFO[georgie.role].label} - ${statusLabel[georgie.status]}</span>
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

function getGeorgieImage(georgie, mode) {
  if (georgie.role === "little") {
    return `./assets/images/georgie-${georgie.status}.png`;
  }

  const suffix = mode === "avatar" ? "-avatar" : "";
  return `./assets/images/${georgie.role}-${georgie.status}${suffix}.png`;
}
