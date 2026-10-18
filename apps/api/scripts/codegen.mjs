// Writes Wolverine's handler code to src/Api/Internal/Generated (W10), then names each file as a generated C# file, *.g.cs. The folder
// is removed first, so a handler that is gone leaves no file. The host compiles the files and loads the types by name, so the file
// name changes nothing at run time (HandlerCodeFileTests). Run it from apps/api: pnpm codegen
import { execFileSync } from "node:child_process";
import { readdirSync, renameSync, rmSync } from "node:fs";
import { join } from "node:path";

const folder = "src/Api/Internal/Generated";

rmSync(folder, { recursive: true, force: true });
execFileSync("dotnet", ["run", "--project", "src/Api", "--", "codegen", "write"], { stdio: "inherit" });

for (const file of readdirSync(folder, { recursive: true })) {
  if (file.endsWith(".cs") && !file.endsWith(".g.cs")) {
    renameSync(join(folder, file), join(folder, `${file.slice(0, -".cs".length)}.g.cs`));
  }
}
