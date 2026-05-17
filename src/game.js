export const MAX_DAY = 18;

export const ACTIONS = [
  {
    id: "share",
    title: "Share the crop",
    label: "Feed and rest",
    cost: { apples: 4 },
    summary: "Spend apples to restore the most exhausted Georgies.",
    apply(state) {
      const next = cloneState(state);
      next.apples -= 4;
      improveGeorgies(next, 2);
      next.log.unshift("The harvest was shared before anyone could hoard it.");
      return next;
    },
    canUse(state) {
      return state.apples >= 4 && state.georgies.some((georgie) => georgie.status !== "happy");
    }
  },
  {
    id: "rent",
    title: "Capture land rent",
    label: "Fund the commons",
    cost: {},
    summary: "Move site rent into the common fund and lower tomorrow's drain.",
    apply(state) {
      const next = cloneState(state);
      const captured = 3 + next.orchards;
      next.commons += captured;
      next.rentDrain = Math.max(1, next.rentDrain - 1);
      next.log.unshift(`Collected ${captured} rent for the common fund.`);
      return next;
    },
    canUse() {
      return true;
    }
  },
  {
    id: "orchard",
    title: "Plant commons",
    label: "Grow an orchard",
    cost: { commons: 6 },
    summary: "Invest the fund in another shared apple grove.",
    apply(state) {
      const next = cloneState(state);
      next.commons -= 6;
      next.orchards += 1;
      next.log.unshift("A new common orchard took root.");
      return next;
    },
    canUse(state) {
      return state.commons >= 6;
    }
  },
  {
    id: "push",
    title: "Push the harvest",
    label: "Work harder",
    cost: {},
    summary: "Gain apples now, but exhaustion spreads through the crew.",
    apply(state) {
      const next = cloneState(state);
      next.apples += 5;
      degradeGeorgies(next, 2);
      next.log.unshift("The Georgies worked late into the dusk.");
      return next;
    },
    canUse() {
      return true;
    }
  }
];

export const EVENT_DECK = [
  {
    title: "Apples after rain",
    body: "Soft rain fattened the fruit. The happy gatherers hum under the leaves.",
    apples: 3,
    commons: 0,
    fatigue: 0
  },
  {
    title: "A landlord's notice",
    body: "Someone claims the best trees and asks for rent before breakfast.",
    apples: 0,
    commons: 0,
    rentDrain: 1,
    fatigue: 1
  },
  {
    title: "A neighbor brings tools",
    body: "A borrowed cart makes the day's gathering easier.",
    apples: 2,
    commons: 1,
    fatigue: 0
  },
  {
    title: "Dry ground",
    body: "The old orchard gives less today, and tired feet drag in the dust.",
    apples: -1,
    commons: 0,
    fatigue: 1
  },
  {
    title: "Commons meeting",
    body: "The Georgies agree that the land's value belongs to everyone.",
    apples: 0,
    commons: 3,
    rentDrain: -1,
    fatigue: 0
  }
];

export function createInitialState() {
  return {
    day: 1,
    apples: 9,
    commons: 4,
    orchards: 2,
    rentDrain: 3,
    lastEvent: EVENT_DECK[0],
    georgies: [
      { id: 1, name: "Pip", status: "happy" },
      { id: 2, name: "Mara", status: "happy" },
      { id: 3, name: "Nell", status: "happy" },
      { id: 4, name: "Bo", status: "tired" },
      { id: 5, name: "Ira", status: "tired" },
      { id: 6, name: "Tuck", status: "happy" }
    ],
    log: ["The Little Georgies enter the orchard."]
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

export function getScore(state) {
  const happy = countStatus(state, "happy");
  const tired = countStatus(state, "tired");
  const broken = countStatus(state, "broken");
  return Math.max(0, happy * 12 + tired * 6 + state.commons * 2 + state.orchards * 5 - broken * 10 - state.rentDrain * 3);
}

export function countStatus(state, status) {
  return state.georgies.filter((georgie) => georgie.status === status).length;
}

export function getHarvest(state) {
  const base = state.georgies.reduce((total, georgie) => {
    if (georgie.status === "happy") return total + 2;
    if (georgie.status === "tired") return total + 2;
    return total + 1;
  }, 0);

  return base + state.orchards;
}

export function applyAction(state, actionId) {
  const action = ACTIONS.find((candidate) => candidate.id === actionId);
  if (!action) {
    throw new Error(`Unknown action: ${actionId}`);
  }

  if (!action.canUse(state)) {
    return cloneState(state);
  }

  return action.apply(state);
}

export function endDay(state) {
  const next = cloneState(state);
  const event = EVENT_DECK[(next.day + next.orchards + next.rentDrain) % EVENT_DECK.length];
  const harvest = getHarvest(next);
  const foodNeed = next.georgies.length;
  const rent = next.rentDrain;
  const eventApples = event.apples ?? 0;

  next.apples += harvest + eventApples;
  next.commons += event.commons ?? 0;
  next.rentDrain = Math.max(1, next.rentDrain + (event.rentDrain ?? 0));
  next.apples -= foodNeed + rent;

  if (next.apples < 0) {
    const shortage = Math.abs(next.apples);
    next.apples = 0;
    degradeGeorgies(next, Math.min(next.georgies.length, shortage));
    next.log.unshift(`Short by ${shortage} apples after food and rent.`);
  } else {
    recoverFromSurplus(next);
    next.log.unshift(`Gathered ${harvest} apples, then paid ${foodNeed + rent} for food and rent.`);
  }

  if (event.fatigue) {
    degradeGeorgies(next, event.fatigue);
  }

  next.lastEvent = event;
  next.day += 1;
  return next;
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
      title: "The orchard went quiet",
      body: "Every Little Georgie broke under hunger and rent. Next time, build the commons before the drain eats the harvest.",
      score,
      happy,
      broken
    };
  }

  if (score >= 90 && broken === 0) {
    return {
      kicker: "Season complete",
      title: "The commons flourished",
      body: "The apples fed the gatherers, the rent filled the common fund, and the Little Georgies ended the season standing tall.",
      score,
      happy,
      broken
    };
  }

  if (score >= 60) {
    return {
      kicker: "Season complete",
      title: "The settlement held",
      body: "The Little Georgies made it through. A few were worn down, but the shared orchard is still alive for another season.",
      score,
      happy,
      broken
    };
  }

  return {
    kicker: "Season complete",
    title: "A hard lesson",
    body: "The Georgies survived, but rent and exhaustion took too much. A stronger common fund would change the next season.",
    score,
    happy,
    broken
  };
}

function improveGeorgies(state, count) {
  const priority = ["broken", "tired"];
  let remaining = count;

  for (const status of priority) {
    for (const georgie of state.georgies) {
      if (remaining === 0) return;
      if (georgie.status === status) {
        georgie.status = status === "broken" ? "tired" : "happy";
        remaining -= 1;
      }
    }
  }
}

function degradeGeorgies(state, count) {
  const priority = ["tired", "happy"];
  let remaining = count;

  for (const status of priority) {
    for (const georgie of [...state.georgies].reverse()) {
      if (remaining === 0) return;
      if (georgie.status === status) {
        georgie.status = status === "happy" ? "tired" : "broken";
        remaining -= 1;
      }
    }
  }
}

function recoverFromSurplus(state) {
  if (state.apples < 5) return;
  const tired = state.georgies.find((georgie) => georgie.status === "tired");
  if (!tired) return;
  tired.status = "happy";
  state.apples -= 2;
  state.log.unshift("A fed, rested Georgie felt happy again.");
}
