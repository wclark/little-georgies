export const GROWTH_HAPPY_RATE_TARGET = 0.5;
export const SPECIALIST_HAPPY_RATE_TARGET = 0.5;
export const SPECIALIST_APPLE_TARGET = 30;
export const SPECIALIST_POPULATION_TARGET = 5;

export const LITTLE_NAMES = ["Ada", "Mara", "Nell", "Bo", "Ira", "Tuck", "Lio", "Fern", "Sol", "June"];

export const ROLE_INFO = {
  little: {
    label: "Little Georgie",
    plural: "Little Georgies",
    workLabel: "Gather apples",
    restLabel: "Rest",
    workSummary: "Collects apples from the orchard.",
    restSummary: "Does not gather, but can become happy after eating."
  },
  chief: {
    label: "Chief Georgie",
    plural: "Chief Georgies",
    workLabel: "Lead sharing",
    restLabel: "Rest",
    workSummary: "Levy taxes and redistribute apples before dinner.",
    restSummary: "Recover enough to keep the village together."
  },
  farmer: {
    label: "Farmer Georgie",
    plural: "Farmer Georgies",
    workLabel: "Harvest",
    restLabel: "Rest",
    workSummary: "Gather apples. Baskets increase a fed farmer's yield.",
    restSummary: "Recover after eating from the pantry."
  },
  builder: {
    label: "Builder Georgie",
    plural: "Builder Georgies",
    workLabel: "Build",
    restLabel: "Rest",
    workSummary: "Make baskets and add progress toward houses.",
    restSummary: "Recover after eating from the pantry."
  }
};

const GROWTH_TARGETS = [0, 4, 10, 18, 26];
const HOUSE_PROGRESS_TARGET = 10;

export function createInitialState() {
  return {
    phase: "solo",
    day: 1,
    apples: 0,
    totalApples: 0,
    happyTurns: 0,
    moodTurns: 0,
    growthHappyTurns: 0,
    growthMoodTurns: 0,
    baskets: 0,
    houses: 0,
    houseProgress: 0,
    commons: 0,
    nextId: 2,
    rolePlans: {
      chief: "work",
      farmer: "work",
      builder: "work"
    },
    lastEvent: {
      title: "One Little Georgie",
      body: "Choose work to gather apples or rest to recover. After the choice, the pantry decides tomorrow's mood."
    },
    georgies: [
      {
        id: 1,
        name: "Little Georgie",
        role: "little",
        status: "happy",
        plan: "work",
        isNew: true
      }
    ],
    log: ["One happy Little Georgie arrives at the orchard."]
  };
}

export function cloneState(state) {
  return {
    ...state,
    rolePlans: { ...state.rolePlans },
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

export function setRolePlan(state, role, plan) {
  if (!["chief", "farmer", "builder"].includes(role)) {
    throw new Error(`Unknown role: ${role}`);
  }

  if (!["work", "rest"].includes(plan)) {
    throw new Error(`Unknown plan: ${plan}`);
  }

  const next = cloneState(state);
  next.rolePlans[role] = plan;
  return next;
}

export function advanceDay(state) {
  const next = cloneState(state);
  const notes = [];
  let gathered = 0;
  let basketsMade = 0;
  let houseProgressMade = 0;
  let chiefWorked = false;

  for (const georgie of next.georgies) {
    const plan = getPlanForGeorgie(next, georgie);
    if (plan !== "work") continue;

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
  next.totalApples += gathered;

  if (next.phase === "village") {
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
  }

  const food = feedAndUpdateStatuses(next);
  const happyCount = countStatus(next, "happy");
  next.happyTurns += happyCount;
  next.moodTurns += next.georgies.length;
  next.growthHappyTurns += happyCount;
  next.growthMoodTurns += next.georgies.length;

  if (next.phase !== "village") {
    maybeGrowLittlePopulation(next, notes);
    maybeEnterSpecialistPhase(next, notes);
  }

  next.day += 1;
  next.lastEvent = getNextEvent(next, { gathered, basketsMade, houseProgressMade, food });
  next.log = [
    summarizeDay(next, { gathered, basketsMade, houseProgressMade, food, chiefWorked }),
    ...notes,
    ...next.log
  ].slice(0, 8);

  for (const georgie of next.georgies) {
    georgie.isNew = false;
    if (georgie.role === "little") {
      georgie.plan = "work";
    }
  }

  return next;
}

export function countStatus(state, status) {
  return state.georgies.filter((georgie) => georgie.status === status).length;
}

export function countRole(state, role) {
  return state.georgies.filter((georgie) => georgie.role === role).length;
}

export function getHappyRate(state) {
  if (state.moodTurns === 0) return 1;
  return state.happyTurns / state.moodTurns;
}

export function getGrowthHappyRate(state) {
  if (state.growthMoodTurns === 0) return 1;
  return state.growthHappyTurns / state.growthMoodTurns;
}

export function getNextGrowthTarget(state) {
  if (state.phase === "village") return null;
  if (state.georgies.length >= SPECIALIST_POPULATION_TARGET) return SPECIALIST_APPLE_TARGET;
  return GROWTH_TARGETS[state.georgies.length] ?? SPECIALIST_APPLE_TARGET;
}

export function getGrowthReadiness(state) {
  if (state.phase === "village") return 100;

  const target = getNextGrowthTarget(state) ?? SPECIALIST_APPLE_TARGET;
  const apples = Math.min(1, state.totalApples / target);
  const happyRate = Math.min(1, getGrowthHappyRate(state) / GROWTH_HAPPY_RATE_TARGET);
  return Math.round((apples * 0.55 + happyRate * 0.45) * 100);
}

export function getSpecialistReadiness(state) {
  if (state.phase === "village") return 100;

  const apples = Math.min(1, state.totalApples / SPECIALIST_APPLE_TARGET);
  const happyRate = Math.min(1, getGrowthHappyRate(state) / SPECIALIST_HAPPY_RATE_TARGET);
  const population = Math.min(1, state.georgies.length / SPECIALIST_POPULATION_TARGET);
  return Math.round((apples * 0.36 + happyRate * 0.34 + population * 0.3) * 100);
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
      Math.round(getHappyRate(state) * 35) +
      state.baskets * 4 +
      state.houses * 8 +
      state.commons * 2 -
      broken * 12
  );
}

export function getRoleMoodCounts(state) {
  return ["chief", "farmer", "builder"].map((role) => {
    const roleGeorgies = state.georgies.filter((georgie) => georgie.role === role);
    return {
      role,
      total: roleGeorgies.length,
      happy: roleGeorgies.filter((georgie) => georgie.status === "happy").length,
      tired: roleGeorgies.filter((georgie) => georgie.status === "tired").length,
      broken: roleGeorgies.filter((georgie) => georgie.status === "broken").length
    };
  });
}

export function isSeasonOver(state) {
  return countStatus(state, "broken") >= state.georgies.length;
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
    title: state.phase === "solo" ? "Still one Little Georgie" : "Still Little Georgies",
    body: "The orchard survived, but did not build enough happy turns and aggregate apples to organize the specialist village.",
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

function getPlanForGeorgie(state, georgie) {
  if (state.phase === "village" && georgie.role !== "little") {
    return state.rolePlans[georgie.role] ?? "work";
  }

  return georgie.plan;
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
  const chiefHappy = state.georgies.filter((georgie) => georgie.role === "chief" && georgie.status === "happy").length;
  const chiefTired = state.georgies.filter((georgie) => georgie.role === "chief" && georgie.status === "tired").length;
  const levyLimit = chiefHappy * 2 + chiefTired;
  const levied = Math.min(levyLimit, state.apples);
  state.apples -= levied;
  state.commons += levied;

  const hungryEstimate = Math.max(0, state.georgies.length - state.apples);
  const redistributed = Math.min(state.commons, hungryEstimate);
  state.commons -= redistributed;
  state.apples += redistributed;

  if (levied > 0 || redistributed > 0) {
    notes.push(`Chief Georgies levied ${levied} and redistributed ${redistributed} apples.`);
  }
}

function feedAndUpdateStatuses(state) {
  let ate = 0;
  let hungry = 0;

  for (const georgie of state.georgies) {
    const plan = getPlanForGeorgie(state, georgie);
    if (state.apples > 0) {
      state.apples -= 1;
      ate += 1;
      georgie.status = plan === "rest" ? "happy" : "tired";
    } else {
      hungry += 1;
      georgie.status = "broken";
    }
  }

  return { ate, hungry };
}

function maybeGrowLittlePopulation(state, notes) {
  if (state.georgies.length >= SPECIALIST_POPULATION_TARGET) return;
  if (countStatus(state, "broken") > 0) return;
  if (getGrowthHappyRate(state) < GROWTH_HAPPY_RATE_TARGET) return;
  if (state.totalApples < getNextGrowthTarget(state)) return;

  if (state.phase === "solo") {
    state.phase = "band";
    state.georgies[0].name = "Henry";
  }

  const name = pickLittleName(state);
  state.georgies.push({
    id: state.nextId,
    name,
    role: "little",
    status: "happy",
    plan: "work",
    isNew: true
  });
  state.nextId += 1;
  state.growthHappyTurns = 0;
  state.growthMoodTurns = 0;
  notes.push(`${name} joined as a happy Little Georgie.`);
}

function maybeEnterSpecialistPhase(state, notes) {
  if (state.phase === "solo") return;
  if (state.totalApples < SPECIALIST_APPLE_TARGET) return;
  if (getGrowthHappyRate(state) < SPECIALIST_HAPPY_RATE_TARGET) return;
  if (state.georgies.length < SPECIALIST_POPULATION_TARGET) return;

  const population = state.georgies.length;
  const specialists = [];
  specialists.push(...createAnonymousGeorgies("chief", 1, state.nextId));
  specialists.push(...createAnonymousGeorgies("farmer", Math.max(1, Math.ceil((population - 1) * 0.6)), state.nextId + specialists.length));
  specialists.push(
    ...createAnonymousGeorgies(
      "builder",
      Math.max(1, population - specialists.length),
      state.nextId + specialists.length
    )
  );

  state.phase = "village";
  state.baskets = 1;
  state.houses = 1;
  state.commons = 2;
  state.georgies = specialists;
  state.nextId += specialists.length;
  state.growthHappyTurns = 0;
  state.growthMoodTurns = 0;
  notes.push("The named Little Georgies organized into anonymous Chief, Farmer, and Builder groups.");
}

function createAnonymousGeorgies(role, count, startingId) {
  return Array.from({ length: count }, (_, index) => ({
    id: startingId + index,
    name: ROLE_INFO[role].label,
    role,
    status: "happy",
    plan: "work",
    isNew: true
  }));
}

function pickLittleName(state) {
  const used = new Set(state.georgies.map((georgie) => georgie.name.toLowerCase()));
  const pool = LITTLE_NAMES.filter((name) => !used.has(name.toLowerCase()));
  if (pool.length === 0) return `Georgie ${state.nextId}`;
  return pool[Math.floor(Math.random() * pool.length)];
}

function getNextEvent(state, result) {
  if (state.phase === "village") {
    return {
      title: "Anonymous specialists",
      body: "The village now tracks Chief, Farmer, and Builder Georgies by role and mood counts."
    };
  }

  if (state.phase === "solo") {
    return {
      title: "One Little Georgie",
      body: "Work gathers apples. Rest can make a fed Little Georgie happy again."
    };
  }

  if (result.food.hungry > 0) {
    return {
      title: "An empty pantry",
      body: "Any Little Georgie who cannot eat an apple becomes broken on the next morning."
    };
  }

  if (state.georgies.some((georgie) => georgie.isNew)) {
    return {
      title: "The band grows",
      body: "A happy-turn rate and aggregate apple harvest brought another named Little Georgie."
    };
  }

  if (getSpecialistReadiness(state) >= 75) {
    return {
      title: "Almost a village",
      body: "Enough named Little Georgies, happy turns, and aggregate apples will unlock the three specialist groups."
    };
  }

  return {
    title: "Named Little Georgies",
    body: "Keep the band fed and happy while the aggregate apple harvest grows."
  };
}

function summarizeDay(state, result) {
  const pieces = [];

  if (result.gathered > 0) pieces.push(`gathered ${result.gathered} apples`);
  if (state.phase === "village" && result.basketsMade > 0) pieces.push(`made ${result.basketsMade} baskets`);
  if (state.phase === "village" && result.houseProgressMade > 0) pieces.push(`built ${result.houseProgressMade} house progress`);
  if (state.phase === "village" && result.chiefWorked) pieces.push("shared under Chief Georgies");
  if (pieces.length === 0) pieces.push("rested");

  return `Day ${state.day - 1}: ${pieces.join(", ")}. ${result.food.ate} ate, ${result.food.hungry} went hungry.`;
}
