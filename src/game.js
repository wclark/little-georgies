export const GROWTH_HAPPY_RATE_TARGET = 0.5;
export const SPECIALIST_HAPPY_RATE_TARGET = 0.5;
export const SPECIALIST_APPLE_TARGET = 30;
export const SPECIALIST_POPULATION_TARGET = 5;
export const HAPPY_RATE_WINDOW_DAYS = 10;

export const LITTLE_NAMES = ["Ada", "Mara", "Nell", "Bo", "Ira", "Tuck", "Lio", "Fern", "Sol", "June"];

const STATUS_SCORE = {
  broken: 0,
  tired: 1,
  happy: 2
};

const STATUS_BY_SCORE = ["broken", "tired", "happy"];
const RESOURCE_TYPES = ["apples", "baskets", "houses"];

const DEFAULT_WORK_PLAN = {
  little: "work",
  chief: "work",
  farmer: "work",
  builder: "basket"
};

const PLAN_OPTIONS = {
  little: ["work", "rest"],
  chief: ["work", "rest"],
  farmer: ["work", "rest"],
  builder: ["basket", "house", "rest"]
};

const DEFAULT_CHIEF_POLICY = {
  levy: {
    apples: true,
    baskets: false,
    houses: false
  },
  distribute: {
    apples: true,
    baskets: true,
    houses: true
  }
};

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
    workLabel: "Administer",
    restLabel: "Rest",
    workSummary: "Levy and distribute apples, baskets, and houses before dinner.",
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
    workLabel: "Make basket",
    houseLabel: "Build house",
    restLabel: "Rest",
    workSummary: "Make baskets or add progress toward houses.",
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
    moodHistory: [],
    baskets: 0,
    houses: 0,
    houseProgress: 0,
    commons: 0,
    nextId: 2,
    chiefPolicy: getDefaultChiefPolicy(),
    rolePlans: {
      chief: "work",
      farmer: "work",
      builder: "basket"
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
        hasBasket: false,
        hasHouse: false,
        isNew: true
      }
    ],
    log: ["One happy Little Georgie arrives at the orchard."]
  };
}

export function cloneState(state) {
  return {
    ...state,
    chiefPolicy: cloneChiefPolicy(state.chiefPolicy),
    rolePlans: { ...state.rolePlans },
    lastEvent: { ...state.lastEvent },
    georgies: state.georgies.map((georgie) => ({ ...georgie })),
    moodHistory: [...(state.moodHistory ?? [])],
    log: [...state.log]
  };
}

export function setPlan(state, georgieId, plan) {
  const next = cloneState(state);
  const georgie = next.georgies.find((candidate) => candidate.id === georgieId);
  if (!georgie) {
    throw new Error(`Unknown Georgie: ${georgieId}`);
  }

  georgie.plan = normalizePlanForGeorgie(georgie, plan);
  return next;
}

export function setRolePlan(state, role, plan) {
  if (!["chief", "farmer", "builder"].includes(role)) {
    throw new Error(`Unknown role: ${role}`);
  }

  if (!PLAN_OPTIONS[role].includes(plan)) {
    throw new Error(`Unknown plan: ${plan}`);
  }

  const next = cloneState(state);
  next.rolePlans[role] = plan;
  for (const georgie of next.georgies) {
    if (georgie.role === role) {
      georgie.plan = normalizePlanForGeorgie(georgie, plan);
    }
  }
  return next;
}

export function setChiefPolicy(state, category, resource, enabled) {
  if (!["levy", "distribute"].includes(category)) {
    throw new Error(`Unknown chief policy category: ${category}`);
  }

  if (!RESOURCE_TYPES.includes(resource)) {
    throw new Error(`Unknown chief policy resource: ${resource}`);
  }

  const next = cloneState(state);
  next.chiefPolicy[category][resource] = Boolean(enabled);
  return next;
}

export function advanceDay(state) {
  const next = cloneState(state);
  const notes = [];
  let gathered = 0;
  let basketsMade = 0;
  let houseProgressMade = 0;
  let housesMade = 0;
  let chiefWorked = false;
  let chiefResult = getEmptyChiefResult();

  for (const georgie of next.georgies) {
    const plan = getPlanForGeorgie(next, georgie);
    georgie.plan = plan;
    if (plan === "rest") continue;

    if (georgie.role === "chief") {
      chiefWorked = true;
      continue;
    }

    if (georgie.role === "builder") {
      const output = getBuilderOutput(georgie.status, plan);
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
      housesMade += 1;
      notes.push("A new house was finished.");
    }

    if (chiefWorked) {
      chiefResult = applyChiefWork(next, notes);
    }
  }

  const food = feedAndUpdateStatuses(next);
  const happyCount = countStatus(next, "happy");
  recordMoodTurn(next, happyCount, next.georgies.length);

  if (next.phase !== "village") {
    maybeGrowLittlePopulation(next, notes);
    maybeEnterSpecialistPhase(next, notes);
  }

  next.day += 1;
  next.lastEvent = getNextEvent(next, { gathered, basketsMade, houseProgressMade, housesMade, food, chiefResult });
  next.log = [
    summarizeDay(next, { gathered, basketsMade, houseProgressMade, housesMade, food, chiefWorked, chiefResult }),
    ...notes,
    ...next.log
  ].slice(0, 8);

  for (const georgie of next.georgies) {
    georgie.isNew = false;
    georgie.plan = normalizePlanForGeorgie(georgie, georgie.plan);
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
  if (state.moodHistory?.length > 0) return getMoodHistoryRate(state.moodHistory);
  if (state.moodTurns === 0) return 1;
  return state.happyTurns / state.moodTurns;
}

export function getGrowthHappyRate(state) {
  if (state.moodHistory?.length > 0) return getMoodHistoryRate(state.moodHistory);
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
      broken: roleGeorgies.filter((georgie) => georgie.status === "broken").length,
      median: getMedianStatus(roleGeorgies)
    };
  });
}

export function getMedianStatus(georgies) {
  if (georgies.length === 0) return "broken";

  const scores = georgies
    .map((georgie) => STATUS_SCORE[georgie.status])
    .filter((score) => Number.isInteger(score))
    .sort((a, b) => a - b);

  if (scores.length === 0) return "broken";

  const middle = Math.floor(scores.length / 2);
  const median = scores.length % 2 === 1 ? scores[middle] : Math.round((scores[middle - 1] + scores[middle]) / 2);
  return STATUS_BY_SCORE[median];
}

export function isSeasonOver(state) {
  return false;
}

export function getEnding(state) {
  const happy = countStatus(state, "happy");
  const broken = countStatus(state, "broken");
  const score = getScore(state);

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
    const hasBasket = typeof georgie.hasBasket === "boolean" ? georgie.hasBasket : baskets > 0;
    return hasBasket ? base + 2 : base;
  }

  if (georgie.status === "broken") return 1;
  return 2;
}

function getPlanForGeorgie(state, georgie) {
  return normalizePlanForGeorgie(georgie, georgie.plan ?? state.rolePlans[georgie.role] ?? DEFAULT_WORK_PLAN[georgie.role]);
}

function getBuilderOutput(status, plan) {
  if (plan === "house") {
    if (status === "broken") return { baskets: 0, houseProgress: 0 };
    return { baskets: 0, houseProgress: status === "happy" ? 2 : 1 };
  }

  if (plan === "basket") {
    return { baskets: status === "happy" ? 2 : 1, houseProgress: 0 };
  }

  return { baskets: 0, houseProgress: 0 };
}

function applyChiefWork(state, notes) {
  const chief = state.georgies.find((georgie) => georgie.role === "chief");
  const capacity = getChiefCapacity(chief?.status);
  const policy = cloneChiefPolicy(state.chiefPolicy);
  const result = getEmptyChiefResult();

  if (policy.levy.apples) {
    result.levied.apples = Math.min(capacity, state.apples);
    state.apples -= result.levied.apples;
    state.commons += result.levied.apples;
  }

  if (policy.levy.baskets) {
    result.levied.baskets = collectOwnedResource(state, "hasBasket", capacity);
    state.baskets += result.levied.baskets;
  }

  if (policy.levy.houses) {
    result.levied.houses = collectOwnedResource(state, "hasHouse", capacity);
    state.houses += result.levied.houses;
  }

  if (policy.distribute.apples) {
    const hungryEstimate = Math.max(0, state.georgies.length - state.apples);
    result.distributed.apples = Math.min(state.commons, hungryEstimate);
    state.commons -= result.distributed.apples;
    state.apples += result.distributed.apples;
  }

  if (policy.distribute.baskets) {
    result.distributed.baskets = distributeOwnedResource(
      state,
      "hasBasket",
      "baskets",
      (georgie) => georgie.role === "farmer",
      capacity
    );
  }

  if (policy.distribute.houses) {
    result.distributed.houses = distributeOwnedResource(
      state,
      "hasHouse",
      "houses",
      (georgie) => georgie.role !== "chief",
      capacity
    );
  }

  const resourceNotes = [];
  for (const resource of RESOURCE_TYPES) {
    if (result.levied[resource] > 0 || result.distributed[resource] > 0) {
      resourceNotes.push(
        `${resource}: levied ${result.levied[resource]}, distributed ${result.distributed[resource]}`
      );
    }
  }

  if (resourceNotes.length > 0) {
    notes.push(`Chief Henry's common fund changed: ${resourceNotes.join("; ")}.`);
  }

  return result;
}

function feedAndUpdateStatuses(state) {
  let ate = 0;
  let hungry = 0;

  for (const georgie of state.georgies) {
    const plan = getPlanForGeorgie(state, georgie);
    if (state.apples > 0) {
      state.apples -= 1;
      ate += 1;
      georgie.status = plan === "rest" || georgie.hasHouse ? "happy" : "tired";
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
    hasBasket: false,
    hasHouse: false,
    isNew: true
  });
  state.nextId += 1;
  notes.push(`${name} joined as a happy Little Georgie.`);
}

function maybeEnterSpecialistPhase(state, notes) {
  if (state.phase === "solo") return;
  if (state.totalApples < SPECIALIST_APPLE_TARGET) return;
  if (getGrowthHappyRate(state) < SPECIALIST_HAPPY_RATE_TARGET) return;
  if (state.georgies.length < SPECIALIST_POPULATION_TARGET) return;

  state.phase = "village";
  state.baskets = 1;
  state.houses = 1;
  state.commons = 2;
  state.chiefPolicy = getDefaultChiefPolicy();
  state.georgies = createSpecialistGeorgies(state.georgies);
  notes.push("Henry became chief while the others organized into Farmer and Builder Georgies.");
}

function createSpecialistGeorgies(georgies) {
  const [chiefSource, ...workers] = georgies;
  const farmerCount = Math.max(1, Math.ceil(workers.length * 0.6));
  const farmers = workers.slice(0, farmerCount);
  const builders = workers.slice(farmerCount);
  const balancedFarmers = builders.length === 0 && farmers.length > 1 ? farmers.slice(0, -1) : farmers;
  const balancedBuilders = builders.length === 0 && farmers.length > 1 ? farmers.slice(-1) : builders;

  return [
    createSpecialistGeorgie(chiefSource, "chief", "Henry"),
    ...balancedFarmers.map((georgie) => createSpecialistGeorgie(georgie, "farmer")),
    ...balancedBuilders.map((georgie) => createSpecialistGeorgie(georgie, "builder"))
  ];
}

function createSpecialistGeorgie(source, role, fallbackName = ROLE_INFO[role].label) {
  return {
    id: source.id,
    name: source.name === "Little Georgie" ? fallbackName : source.name,
    role,
    status: source.status,
    plan: DEFAULT_WORK_PLAN[role],
    hasBasket: false,
    hasHouse: false,
    isNew: true
  };
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
      body: "Food apples feed dinner. Common apples, free baskets, and free houses are stock that Henry can distribute."
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
      body: "Any Little Georgie who cannot eat an apple becomes broken, but the game keeps going at the same stage."
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
      body: "Enough named Little Georgies, rolling happiness, and aggregate apples will unlock the three specialist groups."
    };
  }

  return {
    title: "Named Little Georgies",
    body: "Keep the band fed and happy. Happiness is judged by the last ten days, not the whole run."
  };
}

function summarizeDay(state, result) {
  const pieces = [];

  if (result.gathered > 0) pieces.push(`gathered ${result.gathered} apples`);
  if (state.phase === "village" && result.basketsMade > 0) pieces.push(`made ${result.basketsMade} baskets`);
  if (state.phase === "village" && result.houseProgressMade > 0) pieces.push(`built ${result.houseProgressMade} house progress`);
  if (state.phase === "village" && result.housesMade > 0) pieces.push(`finished ${result.housesMade} houses`);
  if (state.phase === "village" && result.chiefWorked) pieces.push(getChiefSummary(result.chiefResult));
  if (pieces.length === 0) pieces.push("rested");

  return `Day ${state.day - 1}: ${pieces.join(", ")}. ${result.food.ate} ate, ${result.food.hungry} went hungry.`;
}

function recordMoodTurn(state, happy, total) {
  state.moodHistory = [...(state.moodHistory ?? []), { day: state.day, happy, total }].slice(-HAPPY_RATE_WINDOW_DAYS);

  const rolling = getMoodHistoryTotals(state.moodHistory);
  state.happyTurns = rolling.happy;
  state.moodTurns = rolling.total;
  state.growthHappyTurns = rolling.happy;
  state.growthMoodTurns = rolling.total;
}

function getMoodHistoryRate(history) {
  const totals = getMoodHistoryTotals(history);
  if (totals.total === 0) return 1;
  return totals.happy / totals.total;
}

function getMoodHistoryTotals(history = []) {
  return history.reduce(
    (totals, entry) => ({
      happy: totals.happy + entry.happy,
      total: totals.total + entry.total
    }),
    { happy: 0, total: 0 }
  );
}

function normalizePlanForGeorgie(georgie, plan) {
  const options = PLAN_OPTIONS[georgie.role] ?? ["work", "rest"];
  const mappedPlan = georgie.role === "builder" && plan === "work" ? "basket" : plan;

  if (!options.includes(mappedPlan)) {
    throw new Error(`Unknown plan: ${plan}`);
  }

  if (georgie.status === "broken") {
    return DEFAULT_WORK_PLAN[georgie.role] ?? "work";
  }

  return mappedPlan;
}

function getChiefCapacity(status) {
  if (status === "happy") return 2;
  return 1;
}

function collectOwnedResource(state, flag, limit) {
  let collected = 0;
  const owners = state.georgies
    .filter((georgie) => georgie.role !== "chief" && georgie[flag])
    .sort((a, b) => STATUS_SCORE[b.status] - STATUS_SCORE[a.status] || a.id - b.id);

  for (const georgie of owners) {
    if (collected >= limit) break;
    georgie[flag] = false;
    collected += 1;
  }

  return collected;
}

function distributeOwnedResource(state, flag, stockKey, predicate, limit) {
  let distributed = 0;
  const recipients = state.georgies
    .filter((georgie) => !georgie[flag] && predicate(georgie))
    .sort((a, b) => STATUS_SCORE[a.status] - STATUS_SCORE[b.status] || a.id - b.id);

  for (const georgie of recipients) {
    if (distributed >= limit || state[stockKey] <= 0) break;
    georgie[flag] = true;
    state[stockKey] -= 1;
    distributed += 1;
  }

  return distributed;
}

function getChiefSummary(result) {
  const levied = RESOURCE_TYPES.filter((resource) => result.levied[resource] > 0)
    .map((resource) => formatResource(resource, result.levied[resource]))
    .join(", ");
  const distributed = RESOURCE_TYPES.filter((resource) => result.distributed[resource] > 0)
    .map((resource) => formatResource(resource, result.distributed[resource]))
    .join(", ");

  if (!levied && !distributed) return "Chief Henry found nothing to move";
  if (levied && distributed) return `Chief Henry levied ${levied} and distributed ${distributed}`;
  if (levied) return `Chief Henry levied ${levied}`;
  return `Chief Henry distributed ${distributed}`;
}

function formatResource(resource, amount) {
  const singular = {
    apples: "apple",
    baskets: "basket",
    houses: "house"
  }[resource];

  return `${amount} ${amount === 1 ? singular : resource}`;
}

function getDefaultChiefPolicy() {
  return cloneChiefPolicy(DEFAULT_CHIEF_POLICY);
}

function cloneChiefPolicy(policy = DEFAULT_CHIEF_POLICY) {
  return {
    levy: { ...DEFAULT_CHIEF_POLICY.levy, ...(policy.levy ?? {}) },
    distribute: { ...DEFAULT_CHIEF_POLICY.distribute, ...(policy.distribute ?? {}) }
  };
}

function getEmptyChiefResult() {
  return {
    levied: {
      apples: 0,
      baskets: 0,
      houses: 0
    },
    distributed: {
      apples: 0,
      baskets: 0,
      houses: 0
    }
  };
}
