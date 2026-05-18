import test from "node:test";
import assert from "node:assert/strict";
import {
  LITTLE_NAMES,
  advanceDay,
  countRole,
  countStatus,
  createInitialState,
  getAppleYield,
  getEnding,
  getHappyRate,
  getMedianStatus,
  getRoleMoodCounts,
  getSpecialistReadiness,
  isSeasonOver,
  setPlan
} from "../src/game.js";

test("initial state starts in the one-Georgie solo stage with only apples", () => {
  const state = createInitialState();

  assert.equal(state.phase, "solo");
  assert.equal(state.apples, 0);
  assert.equal(state.totalApples, 0);
  assert.equal(state.georgies.length, 1);
  assert.equal(state.georgies[0].name, "Little Georgie");
  assert.equal(countStatus(state, "happy"), 1);
  assert.equal(countRole(state, "little"), 1);
});

test("a working Little Georgie eats and becomes tired", () => {
  const state = createInitialState();
  const next = advanceDay(state);

  assert.equal(next.apples, 1);
  assert.equal(next.totalApples, 2);
  assert.equal(next.georgies[0].status, "tired");
});

test("a resting fed Little Georgie becomes happy", () => {
  const afterWork = advanceDay(createInitialState());
  const plannedRest = setPlan(afterWork, 1, "rest");
  const next = advanceDay(plannedRest);

  assert.equal(next.apples, 0);
  assert.equal(next.georgies[0].status, "happy");
});

test("a Georgie without an apple becomes broken", () => {
  const state = setPlan(createInitialState(), 1, "rest");
  const next = advanceDay(state);

  assert.equal(next.apples, 0);
  assert.equal(next.georgies[0].status, "broken");
});

test("growth renames the first Little Georgie Henry and chooses the next from the name set", () => {
  const state = createInitialState();
  state.apples = 1;
  state.totalApples = 4;
  state.happyTurns = 1;
  state.moodTurns = 3;
  state.georgies[0].plan = "rest";
  const next = advanceDay(state);

  assert.equal(next.phase, "band");
  assert.equal(next.georgies.length, 2);
  assert.equal(next.georgies[0].name, "Henry");
  assert.ok(LITTLE_NAMES.includes(next.georgies[1].name));
  assert.equal(next.georgies[1].status, "happy");
});

test("enough aggregate apples, happy rate, and population unlock anonymous specialist groups", () => {
  const state = createInitialState();
  state.phase = "band";
  state.apples = 12;
  state.totalApples = 30;
  state.happyTurns = 22;
  state.moodTurns = 36;
  state.georgies = [
    { id: 1, name: "Henry", role: "little", status: "happy", plan: "rest", isNew: false },
    { id: 2, name: "Ada", role: "little", status: "happy", plan: "rest", isNew: false },
    { id: 3, name: "Mara", role: "little", status: "happy", plan: "rest", isNew: false },
    { id: 4, name: "Nell", role: "little", status: "happy", plan: "rest", isNew: false },
    { id: 5, name: "Bo", role: "little", status: "happy", plan: "rest", isNew: false }
  ];
  state.nextId = 6;

  const next = advanceDay(state);
  const counts = getRoleMoodCounts(next);

  assert.equal(next.phase, "village");
  assert.equal(countRole(next, "chief"), 1);
  assert.equal(next.georgies.find((georgie) => georgie.role === "chief").name, "Henry");
  assert.ok(countRole(next, "farmer") >= 1);
  assert.ok(countRole(next, "builder") >= 1);
  assert.equal(counts.reduce((total, entry) => total + entry.total, 0), 5);
  assert.equal(getSpecialistReadiness(next), 100);
});

test("median status summarizes mixed groups for category images", () => {
  assert.equal(getMedianStatus([{ status: "broken" }, { status: "happy" }]), "tired");
  assert.equal(getMedianStatus([{ status: "broken" }, { status: "tired" }, { status: "happy" }]), "tired");
  assert.equal(getMedianStatus([{ status: "happy" }, { status: "happy" }, { status: "tired" }]), "happy");
});

test("happy rate is based on aggregate Georgie turns", () => {
  const state = createInitialState();
  state.happyTurns = 3;
  state.moodTurns = 6;

  assert.equal(getHappyRate(state), 0.5);
});

test("farmer yield benefits from baskets while broken farmers cannot use them", () => {
  assert.equal(getAppleYield({ role: "farmer", status: "happy" }, 1), 5);
  assert.equal(getAppleYield({ role: "farmer", status: "tired" }, 1), 4);
  assert.equal(getAppleYield({ role: "farmer", status: "broken" }, 1), 1);
});

test("season ending reports a failure when every Georgie is broken", () => {
  const state = createInitialState();
  state.georgies = state.georgies.map((georgie) => ({ ...georgie, status: "broken" }));

  assert.equal(isSeasonOver(state), true);
  assert.equal(getEnding(state).title, "The pantry went quiet");
});

test("the season does not end just because many days have passed", () => {
  const state = createInitialState();
  state.day = 500;
  state.apples = 10;

  assert.equal(isSeasonOver(state), false);
});
