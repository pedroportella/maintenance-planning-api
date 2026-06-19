#!/usr/bin/env node

import { execFileSync } from "node:child_process";

const terraformDir = "infra/aws";

run(["-chdir=" + terraformDir, "fmt", "-check", "-recursive"]);
run(["-chdir=" + terraformDir, "init", "-backend=false", "-input=false"]);
run(["-chdir=" + terraformDir, "validate", "-no-color"]);

function run(args) {
  console.log(`terraform ${args.join(" ")}`);
  execFileSync("terraform", args, { stdio: "inherit" });
}
