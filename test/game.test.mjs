import test from "node:test";
import assert from "node:assert/strict";
import {
  HAPPY_RATE_WINDOW_DAYS,
  LITTLE_NAMES,
  advanceDay,
  countRole,
  countStatus,
  createInitialState,
  getAppleYield,
  getGeorgieHappyRate,
  getGeorgiesHappyRate,
  getHappyRate,
  getMedianStatus,
  getRoleMoodCounts,
  getSpecialistReadiness,
  isSeasonOver,
  setChiefPolicy,
  setPlan,
  setRolePlan
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

test("happy rate retains only the last ten days once history exists", () => {
  const state = createInitialState();
  state.apples = 1;
  state.georgies[0].status = "tired";
  state.georgies[0].plan = "rest";
  state.georgies[0].moodHistory = Array.from({ length: HAPPY_RATE_WINDOW_DAYS }, (_, index) => ({
    day: index + 1,
    happy: 0,
    total: 1
  }));

  const next = advanceDay(state);

  assert.equal(next.moodHistory.length, HAPPY_RATE_WINDOW_DAYS);
  assert.equal(next.georgies[0].moodHistory.length, HAPPY_RATE_WINDOW_DAYS);
  assert.equal(getHappyRate(next), 1 / HAPPY_RATE_WINDOW_DAYS);
  assert.equal(getGeorgieHappyRate(next.georgies[0]), 1 / HAPPY_RATE_WINDOW_DAYS);
});

test("individual happy histories aggregate into village and group rates", () => {
  const state = createInitialState();
  state.phase = "village";
  state.apples = 2;
  state.georgies = [
    { id: 1, name: "Ada", role: "farmer", status: "happy", plan: "rest", hasBasket: false, hasHouse: false, isNew: false },
    { id: 2, name: "Mara", role: "farmer", status: "happy", plan: "work", hasBasket: false, hasHouse: false, isNew: false }
  ];

  const next = advanceDay(state);

  assert.equal(next.georgies[0].status, "happy");
  assert.equal(next.georgies[1].status, "tired");
  assert.equal(getGeorgieHappyRate(next.georgies[0]), 1);
  assert.equal(getGeorgieHappyRate(next.georgies[1]), 0);
  assert.equal(getGeorgiesHappyRate(next.georgies), 0.5);
  assert.equal(getHappyRate(next), 0.5);
});

test("farmer yield benefits from baskets while broken farmers cannot use them", () => {
  assert.equal(getAppleYield({ role: "farmer", status: "happy", hasBasket: true }, 0), 5);
  assert.equal(getAppleYield({ role: "farmer", status: "tired", hasBasket: true }, 0), 4);
  assert.equal(getAppleYield({ role: "farmer", status: "happy", hasBasket: false }, 10), 3);
  assert.equal(getAppleYield({ role: "farmer", status: "broken", hasBasket: true }, 0), 1);
});

test("role plans batch update specialists while individual plans stay independent", () => {
  const state = createInitialState();
  state.phase = "village";
  state.apples = 20;
  state.baskets = 1;
  state.georgies = [
    { id: 1, name: "Henry", role: "chief", status: "happy", plan: "work", isNew: false },
    { id: 2, name: "Ada", role: "farmer", status: "happy", plan: "work", isNew: false },
    { id: 3, name: "Mara", role: "farmer", status: "happy", plan: "work", isNew: false },
    { id: 4, name: "Bo", role: "builder", status: "happy", plan: "work", isNew: false }
  ];

  const restedFarmers = setRolePlan(state, "farmer", "rest");
  assert.deepEqual(restedFarmers.georgies.filter((georgie) => georgie.role === "farmer").map((georgie) => georgie.plan), [
    "rest",
    "rest"
  ]);

  const mixedFarmers = setPlan(restedFarmers, 2, "work");
  assert.equal(mixedFarmers.georgies.find((georgie) => georgie.id === 2).plan, "work");
  assert.equal(mixedFarmers.georgies.find((georgie) => georgie.id === 3).plan, "rest");

  const next = advanceDay(mixedFarmers);
  assert.equal(next.georgies.find((georgie) => georgie.id === 2).status, "tired");
  assert.equal(next.georgies.find((georgie) => georgie.id === 3).status, "happy");
});

test("role plans batch update named Little Georgies and persist after a day", () => {
  const state = createInitialState();
  state.phase = "band";
  state.apples = 2;
  state.georgies = [
    { id: 1, name: "Henry", role: "little", status: "happy", plan: "work", hasBasket: false, hasHouse: false, moodHistory: [], isNew: false },
    { id: 2, name: "Ada", role: "little", status: "happy", plan: "work", hasBasket: false, hasHouse: false, moodHistory: [], isNew: false }
  ];

  const plannedRest = setRolePlan(state, "little", "rest");
  assert.deepEqual(plannedRest.georgies.map((georgie) => georgie.plan), ["rest", "rest"]);

  const next = advanceDay(plannedRest);
  assert.deepEqual(next.georgies.map((georgie) => georgie.plan), ["rest", "rest"]);
  assert.equal(next.georgies.every((georgie) => georgie.status === "happy"), true);
});

test("broken Georgies are forced into minimal work instead of rest", () => {
  const state = createInitialState();
  state.apples = 1;
  state.georgies[0].status = "broken";

  const plannedRest = setPlan(state, 1, "rest");
  assert.equal(plannedRest.georgies[0].plan, "work");

  const next = advanceDay(plannedRest);
  assert.equal(next.georgies[0].status, "tired");
});

test("builder plans separate basket making from house building", () => {
  const state = createInitialState();
  state.phase = "village";
  state.apples = 10;
  state.baskets = 0;
  state.houses = 0;
  state.houseProgress = 0;
  state.georgies = [
    { id: 1, name: "Henry", role: "chief", status: "happy", plan: "rest", hasBasket: false, hasHouse: false, isNew: false },
    { id: 2, name: "Ada", role: "builder", status: "happy", plan: "basket", hasBasket: false, hasHouse: false, isNew: false },
    { id: 3, name: "Mara", role: "builder", status: "tired", plan: "house", hasBasket: false, hasHouse: false, isNew: false },
    { id: 4, name: "Bo", role: "builder", status: "broken", plan: "house", hasBasket: false, hasHouse: false, isNew: false }
  ];

  const next = advanceDay(state);
  assert.equal(next.baskets, 3);
  assert.equal(next.houseProgress, 1);
  assert.equal(next.georgies.find((georgie) => georgie.id === 4).plan, "basket");
});

test("a house lets a fed worker wake rested after working", () => {
  const state = createInitialState();
  state.phase = "village";
  state.apples = 3;
  state.georgies = [
    { id: 1, name: "Henry", role: "chief", status: "happy", plan: "rest", hasBasket: false, hasHouse: false, isNew: false },
    { id: 2, name: "Ada", role: "farmer", status: "happy", plan: "work", hasBasket: false, hasHouse: true, isNew: false }
  ];

  const next = advanceDay(state);
  assert.equal(next.georgies.find((georgie) => georgie.id === 2).status, "happy");
});

test("Chief Henry can levy and distribute baskets and houses", () => {
  let state = createInitialState();
  state.phase = "village";
  state.apples = 10;
  state.baskets = 1;
  state.houses = 1;
  state.georgies = [
    { id: 1, name: "Henry", role: "chief", status: "happy", plan: "work", hasBasket: false, hasHouse: false, isNew: false },
    { id: 2, name: "Ada", role: "farmer", status: "happy", plan: "work", hasBasket: true, hasHouse: false, isNew: false },
    { id: 3, name: "Mara", role: "builder", status: "happy", plan: "basket", hasBasket: false, hasHouse: true, isNew: false },
    { id: 4, name: "Nell", role: "farmer", status: "happy", plan: "work", hasBasket: false, hasHouse: false, isNew: false },
    { id: 5, name: "Bo", role: "builder", status: "happy", plan: "basket", hasBasket: false, hasHouse: false, isNew: false }
  ];
  state = setChiefPolicy(state, "levy", "apples", false);
  state = setChiefPolicy(state, "distribute", "apples", false);
  state = setChiefPolicy(state, "levy", "baskets", true);
  state = setChiefPolicy(state, "levy", "houses", true);

  const next = advanceDay(state);

  assert.equal(next.georgies.find((georgie) => georgie.id === 4).hasBasket, true);
  assert.equal(next.georgies.filter((georgie) => georgie.hasHouse).length, 2);
  assert.equal(next.houses, 0);
});

test("all broken Georgies stay in the current stage instead of ending the game", () => {
  const state = createInitialState();
  state.georgies = state.georgies.map((georgie) => ({ ...georgie, status: "broken" }));

  assert.equal(isSeasonOver(state), false);

  const next = advanceDay(state);
  assert.equal(next.day, 2);
  assert.equal(next.phase, "solo");
  assert.equal(next.georgies[0].status, "tired");
});

test("the season does not end just because many days have passed", () => {
  const state = createInitialState();
  state.day = 500;
  state.apples = 10;

  assert.equal(isSeasonOver(state), false);
});
