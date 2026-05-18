export const MAX_DAY = 28;
export const SPECIALIST_APPLE_TARGET = 7;
export const SPECIALIST_HARMONY_TARGET = 4;
export const SPECIALIST_POPULATION_TARGET = 3;

export const ROLE_INFO = {
  little: {
    label: "Little Georgie",
    workLabel: "Gather apples",
    restLabel: "Rest",
    workSummary: "Collects apples from the orchard.",
    restSummary: "Does not gather, but can become happy after eating."
  },
  chief: {
    label: "Chief Georgie",
    workLabel: "Lead sharing",
    restLabel: "Rest",
    workSummary: "Levy taxes and redistribute apples before dinner.",
    restSummary: "Recover enough to keep the village together."
  },
  farmer: {
    label: "Farmer Georgie",
    workLabel: "Harvest",
    restLabel: "Rest",
    workSummary: "Gather apples. Baskets increase a fed farmer's yield.",
    restSummary: "Recover after eating from the pantry."
  },
  builder: {
    label: "Builder Georgie",
    workLabel: "Build",
    restLabel: "Rest",
    workSummary: "Make baskets and add progress toward houses.",
    restSummary: "Recover after eating from the pantry."
  }
};

const NAMES = ["Pip", "Mara", "Nell", "Bo", "Ira", "Tuck", "Lio", "Fern"];
const HOUSE_PROGRESS_TARGET = 10;

export function createInitialState() {
  return {
    phase: "little",
    day: 1,
    apples: 0,
    baskets: 0,
    houses: 0,
    houseProgress: 0,
    commons: 0,
    harmony: 0,
    nextId: 2,
    lastEvent: {
      title: "One happy Little Georgie",
      body: "Pip has an empty pantry, a bright morning, and one choice: work or rest."
    },
    georgies: [
      {
        id: 1,
        name: "Pip",
        role: "little",
        status: "happy",
        plan: "work",
        isNew: true
      }
    ],
    log: ["Pip arrives happy and ready to gather."]
  };
}

export function cloneState(state) {
  return {
    ...state,
    lastEvent: { ...state.lastEvent },
    georgies: state.georgies.map((georgie) => ({ ...georgie })),
    log: [...state.log]
  };
}

export function setPlan(state, georgieId, plan) {
  if (!["work", "rest"].includes(plan)) {
    throw new Error(`Unknown plan: ${plan}`);
  }

  const next = cloneState(state);
  const georgie = next.georgies.find((candidate) => candidate.id === georgieId);
  if (!georgie) {
    throw new Error(`Unknown Georgie: ${georgieId}`);
  }

  georgie.plan = plan;
  return next;
}

export function advanceDay(state) {
  const next = cloneState(state);
  const startedWithApples = next.apples;
  const notes = [];
  let gathered = 0;
  let basketsMade = 0;
  let houseProgressMade = 0;
  let chiefWorked = false;

  for (const georgie of next.georgies) {
    if (georgie.plan !== "work") continue;

    if (georgie.role === "chief") {
      chiefWorked = true;
      continue;
    }

    if (georgie.role === "builder") {
      const output = getBuilderOutput(georgie.status);
      basketsMade += output.baskets;
      houseProgressMade += output.houseProgress;
      continue;
    }

    gathered += getAppleYield(georgie, next.baskets);
  }

  next.apples += gathered;
  next.baskets += basketsMade;
  next.houseProgress += houseProgressMade;

  while (next.houseProgress >= HOUSE_PROGRESS_TARGET) {
    next.houseProgress -= HOUSE_PROGRESS_TARGET;
    next.houses += 1;
    notes.push("A new house was finished.");
  }

  if (chiefWorked) {
    applyChiefWork(next, notes);
  }

  const food = feedAndUpdateStatuses(next);
  const happyCount = countStatus(next, "happy");
  const harmonyGain = happyCount / next.georgies.length;
  next.harmony = roundTenths(next.harmony + harmonyGain);

  if (next.phase === "little") {
    maybeEnterSpecialistPhase(next, notes);
    if (next.phase === "little") {
      maybeGrowLittlePopulation(next, notes);
      maybeEnterSpecialistPhase(next, notes);
    }
  }

  next.day += 1;
  next.lastEvent = getNextEvent(next, {
    gathered,
    basketsMade,
    houseProgressMade,
    ate: food.ate,
    hungry: food.hungry,
    startedWithApples
  });
  next.log = [
    summarizeDay(next, { gathered, basketsMade, houseProgressMade, food, chiefWorked }),
    ...notes,
    ...next.log
  ].slice(0, 8);

  for (const georgie of next.georgies) {
    georgie.isNew = false;
    georgie.plan = "work";
  }

  return next;
}

export function countStatus(state, status) {
  return state.georgies.filter((georgie) => georgie.status === status).length;
}

export function countRole(state, role) {
  return state.georgies.filter((georgie) => georgie.role === role).length;
}

export function getReadiness(state) {
  if (state.phase === "village") return 100;

  const apples = Math.min(1, state.apples / SPECIALIST_APPLE_TARGET);
  const harmony = Math.min(1, state.harmony / SPECIALIST_HARMONY_TARGET);
  const population = Math.min(1, state.georgies.length / SPECIALIST_POPULATION_TARGET);
  return Math.round((apples * 0.35 + harmony * 0.35 + population * 0.3) * 100);
}

export function getScore(state) {
  const happy = countStatus(state, "happy");
  const tired = countStatus(state, "tired");
  const broken = countStatus(state, "broken");
  return Math.max(
    0,
    happy * 14 +
      tired * 7 +
      state.apples * 2 +
      state.harmony * 5 +
      state.baskets * 4 +
      state.houses * 8 +
      state.commons * 2 -
      broken * 12
  );
}

export function isSeasonOver(state) {
  return state.day > MAX_DAY || countStatus(state, "broken") >= state.georgies.length;
}

export function getEnding(state) {
  const happy = countStatus(state, "happy");
  const broken = countStatus(state, "broken");
  const score = getScore(state);

  if (broken >= state.georgies.length) {
    return {
      kicker: "Season failed",
      title: "The pantry went quiet",
      body: "Every Georgie broke after going hungry. The next village will need more rest and a deeper apple reserve.",
      score,
      happy,
      broken
    };
  }

  if (state.phase === "village" && score >= 110 && broken === 0) {
    return {
      kicker: "Season complete",
      title: "The village held together",
      body: "The Little Georgies grew into a working village, with leadership, farming, building, and enough apples to keep hope alive.",
      score,
      happy,
      broken
    };
  }

  if (state.phase === "village") {
    return {
      kicker: "Season complete",
      title: "A village takes shape",
      body: "The specialists arrived and the village survived. A steadier rhythm of work, rest, and food would make it flourish.",
      score,
      happy,
      broken
    };
  }

  return {
    kicker: "Season complete",
    title: "Still just Little Georgies",
    body: "The first band survived, but did not save enough apples and happy days to organize the specialist village.",
    score,
    happy,
    broken
  };
}

export function getAppleYield(georgie, baskets) {
  if (georgie.role === "farmer") {
    if (georgie.status === "broken") return 1;
    const base = georgie.status === "happy" ? 3 : 2;
    return baskets > 0 ? base + 2 : base;
  }

  if (georgie.status === "broken") return 1;
  return 2;
}

function getBuilderOutput(status) {
  if (status === "happy") {
    return { baskets: 2, houseProgress: 2 };
  }

  if (status === "tired") {
    return { baskets: 1, houseProgress: 1 };
  }

  return { baskets: 1, houseProgress: 0 };
}

function applyChiefWork(state, notes) {
  const chief = state.georgies.find((georgie) => georgie.role === "chief");
  if (!chief) return;

  const levyLimit = chief.status === "happy" ? 2 : chief.status === "tired" ? 1 : 0;
  const levied = Math.min(levyLimit, state.apples);
  state.apples -= levied;
  state.commons += levied;

  const hungryEstimate = Math.max(0, state.georgies.length - state.apples);
  const redistributed = Math.min(state.commons, hungryEstimate);
  state.commons -= redistributed;
  state.apples += redistributed;

  if (levied > 0 || redistributed > 0) {
    notes.push(`Chief Georgie levied ${levied} and redistributed ${redistributed} apples.`);
  }
}

function feedAndUpdateStatuses(state) {
  let ate = 0;
  let hungry = 0;

  for (const georgie of state.georgies) {
    if (state.apples > 0) {
      state.apples -= 1;
      ate += 1;
      georgie.status = georgie.plan === "rest" ? "happy" : "tired";
    } else {
      hungry += 1;
      georgie.status = "broken";
    }
  }

  return { ate, hungry };
}

function maybeGrowLittlePopulation(state, notes) {
  if (state.georgies.length >= 5) return;
  if (countStatus(state, "broken") > 0) return;

  const growthCost = 3 + state.georgies.length;
  if (state.apples < growthCost) return;

  const name = NAMES[(state.nextId - 1) % NAMES.length];
  state.apples -= growthCost;
  state.georgies.push({
    id: state.nextId,
    name,
    role: "little",
    status: "happy",
    plan: "work",
    isNew: true
  });
  state.nextId += 1;
  notes.push(`${name} joined as a happy Little Georgie.`);
}

function maybeEnterSpecialistPhase(state, notes) {
  if (state.apples < SPECIALIST_APPLE_TARGET) return;
  if (state.harmony < SPECIALIST_HARMONY_TARGET) return;
  if (state.georgies.length < SPECIALIST_POPULATION_TARGET) return;

  state.phase = "village";
  state.baskets = Math.max(1, state.baskets);
  state.houses = Math.max(1, state.houses);
  state.commons += 2;
  state.georgies = [
    { id: state.nextId, name: "Chief", role: "chief", status: "happy", plan: "work", isNew: true },
    { id: state.nextId + 1, name: "Farmer", role: "farmer", status: "happy", plan: "work", isNew: true },
    { id: state.nextId + 2, name: "Builder", role: "builder", status: "happy", plan: "work", isNew: true }
  ];
  state.nextId += 3;
  notes.push("The Little Georgies organized into Chief, Farmer, and Builder Georgies.");
}

function getNextEvent(state, result) {
  if (state.phase === "village") {
    return {
      title: "The specialist village",
      body: "Chief, Farmer, and Builder Georgies now need coordinated work, rest, food, baskets, and houses."
    };
  }

  if (result.hungry > 0) {
    return {
      title: "An empty pantry",
      body: "A Georgie who cannot eat an apple becomes broken on the next morning."
    };
  }

  if (state.georgies.some((georgie) => georgie.isNew)) {
    return {
      title: "A new happy face",
      body: "Saved apples made room for another Little Georgie to join the orchard."
    };
  }

  if (getReadiness(state) >= 75) {
    return {
      title: "Almost a village",
      body: "Enough happy days and saved apples will invite Chief, Farmer, and Builder Georgies."
    };
  }

  return {
    title: "Work, rest, eat",
    body: "A working Georgie who eats becomes tired. A resting Georgie who eats becomes happy."
  };
}

function summarizeDay(state, result) {
  const pieces = [];

  if (result.gathered > 0) pieces.push(`gathered ${result.gathered} apples`);
  if (result.basketsMade > 0) pieces.push(`made ${result.basketsMade} baskets`);
  if (result.houseProgressMade > 0) pieces.push(`built ${result.houseProgressMade} house progress`);
  if (result.chiefWorked) pieces.push("shared under Chief Georgie's watch");
  if (pieces.length === 0) pieces.push("rested");

  return `Day ${state.day - 1}: ${pieces.join(", ")}. ${result.food.ate} ate, ${result.food.hungry} went hungry.`;
}

function roundTenths(value) {
  return Math.round(value * 10) / 10;
}
