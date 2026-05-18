import test from "node:test";
import assert from "node:assert/strict";
import {
  advanceDay,
  countRole,
  countStatus,
  createInitialState,
  getAppleYield,
  getEnding,
  getReadiness,
  isSeasonOver,
  setPlan
} from "../src/game.js";

test("initial state starts with one happy Little Georgie and no apples", () => {
  const state = createInitialState();

  assert.equal(state.phase, "little");
  assert.equal(state.apples, 0);
  assert.equal(state.georgies.length, 1);
  assert.equal(countStatus(state, "happy"), 1);
  assert.equal(countRole(state, "little"), 1);
});

test("a working Little Georgie eats and becomes tired", () => {
  const state = createInitialState();
  const next = advanceDay(state);

  assert.equal(next.apples, 1);
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

test("saved apples can grow the Little Georgie population", () => {
  const state = createInitialState();
  state.apples = 5;
  const next = advanceDay(state);

  assert.equal(next.georgies.length, 2);
  assert.equal(next.georgies[1].status, "happy");
});

test("enough apples, harmony, and population unlock the specialist village", () => {
  const state = createInitialState();
  state.apples = 13;
  state.harmony = 4;
  state.georgies = [
    { id: 1, name: "Pip", role: "little", status: "happy", plan: "rest", isNew: false },
    { id: 2, name: "Mara", role: "little", status: "happy", plan: "rest", isNew: false },
    { id: 3, name: "Nell", role: "little", status: "happy", plan: "rest", isNew: false }
  ];
  state.nextId = 4;

  const next = advanceDay(state);

  assert.equal(next.phase, "village");
  assert.equal(countRole(next, "chief"), 1);
  assert.equal(countRole(next, "farmer"), 1);
  assert.equal(countRole(next, "builder"), 1);
  assert.equal(getReadiness(next), 100);
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
