import { cpSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(process.cwd());
const dist = resolve(root, "dist");
const gameDist = resolve(dist, "little-georgies");

if (existsSync(dist)) {
  rmSync(dist, { recursive: true, force: true });
}

mkdirSync(gameDist, { recursive: true });

cpSync(resolve(root, "site/index.html"), resolve(dist, "index.html"));

for (const entry of ["index.html", "src", "assets"]) {
  cpSync(resolve(root, entry), resolve(gameDist, entry), { recursive: true });
}

rmSync(resolve(gameDist, "assets/images/little-georgie-reference.png"), { force: true });

console.log(`Built static site in ${dist}`);
