import test from "node:test";
import assert from "node:assert/strict";
import {
  applyAction,
  countStatus,
  createInitialState,
  endDay,
  getEnding,
  getHarvest,
  isSeasonOver
} from "../src/game.js";

test("initial state has a working crew and harvest", () => {
  const state = createInitialState();

  assert.equal(state.georgies.length, 6);
  assert.equal(countStatus(state, "happy"), 4);
  assert.equal(countStatus(state, "tired"), 2);
  assert.equal(getHarvest(state), 14);
});

test("sharing the crop improves exhausted Georgies", () => {
  const state = createInitialState();
  const next = applyAction(state, "share");

  assert.equal(next.apples, 5);
  assert.equal(countStatus(next, "happy"), 6);
  assert.equal(countStatus(next, "tired"), 0);
});

test("capturing rent grows the common fund and lowers drain", () => {
  const state = createInitialState();
  const next = applyAction(state, "rent");

  assert.equal(next.commons, 9);
  assert.equal(next.rentDrain, 2);
});

test("ending a day advances time and applies food and rent costs", () => {
  const state = createInitialState();
  const next = endDay(state);

  assert.equal(next.day, 2);
  assert.ok(next.apples >= 0);
  assert.ok(next.log.length > state.log.length);
});

test("season ending reports a failure when every Georgie is broken", () => {
  const state = createInitialState();
  state.georgies = state.georgies.map((georgie) => ({ ...georgie, status: "broken" }));

  assert.equal(isSeasonOver(state), true);
  assert.equal(getEnding(state).title, "The orchard went quiet");
});
