import {
  ACTIONS,
  MAX_DAY,
  applyAction,
  countStatus,
  createInitialState,
  endDay,
  getEnding,
  getScore,
  isSeasonOver
} from "./game.js";

const statusImage = {
  happy: "./assets/images/georgie-happy.png",
  tired: "./assets/images/georgie-tired.png",
  broken: "./assets/images/georgie-broken.png"
};

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
const scoreMeter = document.querySelector("[data-bind-style='score']");

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
    state = endDay(state);
    finishOrRender();
    return;
  }

  if (action.startsWith("policy:")) {
    state = applyAction(state, action.replace("policy:", ""));
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
  setText("apples", state.apples);
  setText("commons", state.commons);
  setText("orchards", state.orchards);
  setText("rentDrain", state.rentDrain);
  setText("score", getScore(state));
  setText("eventTitle", state.lastEvent.title);
  setText("eventBody", state.lastEvent.body);

  const score = Math.min(100, getScore(state));
  scoreMeter.style.width = `${score}%`;

  renderActions();
  renderGeorgies();
  renderLog();
}

function renderActions() {
  const actionButtons = ACTIONS.map((action) => {
    const enabled = action.canUse(state);
    const cost = formatCost(action.cost);
    return `
      <button class="action-card" type="button" data-action="policy:${action.id}" ${enabled ? "" : "disabled"}>
        <span>${action.title}</span>
        <strong>${action.label}</strong>
        <small>${action.summary}</small>
        ${cost ? `<em>${cost}</em>` : ""}
      </button>
    `;
  }).join("");

  actionsEl.innerHTML = `
    ${actionButtons}
    <button class="action-card end-turn" type="button" data-action="end-day">
      <span>Gather apples</span>
      <strong>End day</strong>
      <small>Harvest, feed the crew, and face tomorrow's event.</small>
      <em>Required</em>
    </button>
  `;
}

function renderGeorgies() {
  georgiesEl.innerHTML = state.georgies.map((georgie) => `
    <article class="georgie-card ${georgie.status}">
      <img src="${statusImage[georgie.status]}" alt="">
      <div>
        <strong>${georgie.name}</strong>
        <span>${statusLabel[georgie.status]}</span>
      </div>
    </article>
  `).join("");
}

function renderLog() {
  logEl.innerHTML = state.log.slice(0, 5).map((entry) => `<li>${entry}</li>`).join("");
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

function formatCost(cost = {}) {
  const parts = [];
  if (cost.apples) parts.push(`${cost.apples} apples`);
  if (cost.commons) parts.push(`${cost.commons} fund`);
  return parts.join(", ");
}
