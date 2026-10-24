// The frontend's architecture rules as fitness functions (Ford, Parsons, Kua, Building Evolutionary Architectures, Chapter 2). Each
// rule is a pure function of a folder: it reads files and returns its findings, one line each, `path:line: what`. An app's or a
// package's `architecture.test.ts` calls the rules for its own folders and expects no finding. docs/frontend/ARCHITECTURE.md names
// the rule each function enforces.
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { basename, dirname, extname, join, relative, sep } from "node:path";
import ts from "typescript";

export type Finding = string;

const skippedFolders = new Set([
  "node_modules",
  ".next",
  ".turbo",
  "dist",
  "build",
  "test-results",
  "playwright-report",
]);

/** Every file under a folder, as absolute paths, without dependency and build folders. */
export function filesUnder(root: string): string[] {
  if (!existsSync(root)) return [];
  const files: string[] = [];
  const walk = (folder: string) => {
    for (const name of readdirSync(folder).sort()) {
      if (skippedFolders.has(name)) continue;
      const path = join(folder, name);
      if (statSync(path).isDirectory()) walk(path);
      else files.push(path);
    }
  };
  walk(root);
  return files;
}

/** Every folder under a folder, as absolute paths, without dependency and build folders. */
function foldersUnder(root: string): string[] {
  if (!existsSync(root)) return [];
  const folders: string[] = [];
  const walk = (folder: string) => {
    for (const name of readdirSync(folder).sort()) {
      if (skippedFolders.has(name)) continue;
      const path = join(folder, name);
      if (statSync(path).isDirectory()) {
        folders.push(path);
        walk(path);
      }
    }
  };
  walk(root);
  return folders;
}

const isGenerated = (path: string) => /\.gen\.(ts|css)$/.test(path);
const isTest = (path: string) => /\.test\.(ts|tsx|mts|mjs)$/.test(path);
const isCode = (path: string) =>
  /\.(ts|tsx)$/.test(path) && !path.endsWith(".d.ts");

export type Options = {
  /** Paths, relative to the root, that the rule skips: a file, or a folder ending in `/`. */
  ignore?: string[];
};

function ignored(root: string, path: string, options: Options): boolean {
  const relativePath = relative(root, path).split(sep).join("/");
  return (options.ignore ?? []).some((entry) =>
    entry.endsWith("/")
      ? relativePath.startsWith(entry)
      : relativePath === entry,
  );
}

/** The text of a file with its comments blanked out, so a word in a comment ("right-to-left") is no finding. Lines keep their numbers. */
function withoutComments(text: string): string {
  return text.replace(/\/\*[\s\S]*?\*\/|(?<![:"'`])\/\/[^\n]*/g, (comment) =>
    comment.replace(/[^\n]/g, " "),
  );
}

function linesMatching(
  root: string,
  path: string,
  pattern: RegExp,
  what: (match: string) => string,
): Finding[] {
  const findings: Finding[] = [];
  withoutComments(readFileSync(path, "utf8"))
    .split("\n")
    .forEach((text, index) => {
      for (const match of text.matchAll(pattern)) {
        findings.push(
          `${relative(root, path)}:${index + 1}: ${what(match[0].trim())}`,
        );
      }
    });
  return findings;
}

const physicalClass =
  /(?<=^|[\s"'`{(:!])-?(?:ml|mr|pl|pr|left|right|rounded-l|rounded-r|border-l|border-r)-[\w[\].%/-]+|(?<=^|[\s"'`{(:!])text-(?:left|right)\b/g;

/** Rule F17: no physical direction class in a .tsx or .css file. Logical classes (`ms-`, `pe-`, `start-`) mirror in RTL. */
export function physicalDirectionClasses(
  root: string,
  options: Options = {},
): Finding[] {
  return filesUnder(root)
    .filter(
      (path) =>
        /\.(tsx|css)$/.test(path) &&
        !isGenerated(path) &&
        !ignored(root, path, options),
    )
    .flatMap((path) =>
      linesMatching(
        root,
        path,
        physicalClass,
        (match) => `physical direction class "${match}"; use its logical form`,
      ),
    );
}

const colorLiteral =
  /(?<![\w&])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{3,4})\b|\brgba?\(/g;

/** Rule F21: no hex colour or rgb() outside the tokens. A colour is a token: `bg-primary`, `var(--primary)`. */
export function colorLiterals(root: string, options: Options = {}): Finding[] {
  return filesUnder(root)
    .filter(
      (path) =>
        /\.(ts|tsx|css|mjs)$/.test(path) &&
        !isGenerated(path) &&
        basename(path) !== "tokens.json" &&
        !ignored(root, path, options),
    )
    .flatMap((path) =>
      linesMatching(
        root,
        path,
        colorLiteral,
        (match) => `colour literal "${match}"; use a token`,
      ),
    );
}

const userFacingAttributes = new Set([
  "title",
  "placeholder",
  "aria-label",
  "alt",
]);
const hasLetter = (text: string) => /\p{L}/u.test(text);

function sourceFile(path: string): ts.SourceFile {
  return ts.createSourceFile(
    path,
    readFileSync(path, "utf8"),
    ts.ScriptTarget.Latest,
    true,
    ts.ScriptKind.TSX,
  );
}

function lineOf(file: ts.SourceFile, node: ts.Node): number {
  return file.getLineAndCharacterOfPosition(node.getStart(file)).line + 1;
}

/**
 * Rule F25: no user-facing text as a literal. JSX text and the `title`, `placeholder`, `aria-label` and `alt` attributes take
 * their text from the message files. A literal without a letter, such as `·` or `/`, is no text.
 */
export function userFacingLiterals(
  root: string,
  options: Options = {},
): Finding[] {
  const findings: Finding[] = [];
  for (const path of filesUnder(root)) {
    if (
      extname(path) !== ".tsx" ||
      isTest(path) ||
      ignored(root, path, options)
    )
      continue;
    const file = sourceFile(path);
    const visit = (node: ts.Node) => {
      if (ts.isJsxText(node) && hasLetter(node.text)) {
        findings.push(
          `${relative(root, path)}:${lineOf(file, node)}: JSX text "${node.text.trim()}"; use a message`,
        );
      }
      if (
        ts.isJsxAttribute(node) &&
        userFacingAttributes.has(node.name.getText(file)) &&
        node.initializer
      ) {
        const value = ts.isJsxExpression(node.initializer)
          ? node.initializer.expression
          : node.initializer;
        if (
          value &&
          (ts.isStringLiteral(value) ||
            ts.isNoSubstitutionTemplateLiteral(value)) &&
          hasLetter(value.text)
        ) {
          findings.push(
            `${relative(root, path)}:${lineOf(file, node)}: ${node.name.getText(file)}="${value.text}"; use a message`,
          );
        }
      }
      ts.forEachChild(node, visit);
    };
    visit(file);
  }
  return findings;
}

/** Rule F35: no `__tests__` folder. */
export function testFolders(root: string): Finding[] {
  return foldersUnder(root)
    .filter((folder) => basename(folder) === "__tests__")
    .map(
      (folder) =>
        `${relative(root, folder)}: a __tests__ folder; put each test beside its file`,
    );
}

/**
 * Rule F35: every `x.test.ts(x)` sits beside a file of the same base name (`x.ts`, `x.tsx`, `x.mjs`). An
 * `architecture.test.ts` tests its whole folder, so it has no such file.
 */
export function testsBesideTheirFile(root: string): Finding[] {
  return filesUnder(root)
    .filter((path) => isTest(path) && basename(path) !== "architecture.test.ts")
    .filter((path) => {
      const base = basename(path).replace(/\.test\.(ts|tsx|mts|mjs)$/, "");
      return !readdirSync(dirname(path)).some(
        (name) =>
          name !== basename(path) && name.replace(/\.[^.]+$/, "") === base,
      );
    })
    .map(
      (path) =>
        `${relative(root, path)}: no file of the same base name beside the test`,
    );
}

function importedModules(file: ts.SourceFile): ts.StringLiteral[] {
  const modules: ts.StringLiteral[] = [];
  const visit = (node: ts.Node) => {
    if (
      (ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) &&
      node.moduleSpecifier
    ) {
      if (ts.isStringLiteral(node.moduleSpecifier))
        modules.push(node.moduleSpecifier);
    }
    if (
      ts.isCallExpression(node) &&
      node.expression.kind === ts.SyntaxKind.ImportKeyword
    ) {
      const [argument] = node.arguments;
      if (argument && ts.isStringLiteral(argument)) modules.push(argument);
    }
    ts.forEachChild(node, visit);
  };
  visit(file);
  return modules;
}

/**
 * Rule F29: a feature never imports from another feature. `root` is an app's `src` folder; `@/` is that folder. Shared
 * code goes to `packages/ui`.
 */
export function crossFeatureImports(root: string): Finding[] {
  const featuresRoot = join(root, "features");
  const featureOf = (path: string) => {
    const inside = relative(featuresRoot, path);
    return inside.startsWith("..") ? undefined : inside.split(sep)[0];
  };
  const findings: Finding[] = [];
  for (const path of filesUnder(featuresRoot)) {
    if (!isCode(path)) continue;
    const feature = featureOf(path);
    const file = sourceFile(path);
    for (const module of importedModules(file)) {
      const target = module.text.startsWith("@/")
        ? join(root, module.text.slice(2))
        : module.text.startsWith(".")
          ? join(dirname(path), module.text)
          : undefined;
      const other = target && featureOf(target);
      if (other && other !== feature) {
        findings.push(
          `${relative(root, path)}:${lineOf(file, module)}: feature ${feature} imports feature ${other}`,
        );
      }
    }
  }
  return findings;
}

/** Rule F32: a file that says it is generated ends in `.gen.ts` or `.gen.css` (D1). */
export function generatedFileNames(root: string): Finding[] {
  return filesUnder(root)
    .filter((path) => /\.(ts|tsx|css|mjs|js)$/.test(path) && !isGenerated(path))
    .filter((path) => {
      const head = readFileSync(path, "utf8")
        .split("\n")
        .slice(0, 6)
        .join("\n");
      return (
        /Generated (by|from) .*|Do not edit manually/.test(head) &&
        /Do not edit/.test(head)
      );
    })
    .map(
      (path) =>
        `${relative(root, path)}: a generated file whose name does not end in .gen.ts or .gen.css`,
    );
}

type Messages = { [key: string]: string | Messages };

/** The keys of a message file, joined by dots: `{ "members": { "invite": "…" } }` gives `members.invite`. */
export function messageKeys(messages: Messages, prefix = ""): string[] {
  return Object.entries(messages).flatMap(([key, value]) =>
    typeof value === "string"
      ? [`${prefix}${key}`]
      : messageKeys(value, `${prefix}${key}.`),
  );
}

function readMessages(path: string): Messages {
  return JSON.parse(readFileSync(path, "utf8")) as Messages;
}

/** Rule F15: the message files of one place (`en.json`, `tr.json`, `ar.json`) have the same keys. */
export function messageKeySets(
  messagesFolder: string,
  languages: readonly string[],
): Finding[] {
  const keysOf = (language: string) =>
    new Set(
      messageKeys(readMessages(join(messagesFolder, `${language}.json`))),
    );
  const all = new Set(languages.flatMap((language) => [...keysOf(language)]));
  return languages.flatMap((language) => {
    const keys = keysOf(language);
    return [...all]
      .filter((key) => !keys.has(key))
      .map((key) => `${language}.json: no key ${key}`);
  });
}

/** The values of a const object that Orval writes for an enum: `export const Language = { en: 'en', ... } as const;`. */
export function enumValues(generatedFile: string, enumName: string): string[] {
  const text = readFileSync(generatedFile, "utf8");
  const block = new RegExp(
    `export const ${enumName} = \\{([^}]*)\\} as const;`,
  ).exec(text);
  if (!block?.[1])
    throw new Error(`${enumName} is not a const object in ${generatedFile}`);
  return [...block[1].matchAll(/:\s*'([^']*)'/g)].map(
    (match) => match[1] ?? "",
  );
}

/** Rule F16: every error code of the contract has a message `errors.<code>` in each shared message file. */
export function errorCodeMessages(
  messagesFolder: string,
  languages: readonly string[],
  errorCodes: readonly string[],
): Finding[] {
  return languages.flatMap((language) => {
    const keys = new Set(
      messageKeys(readMessages(join(messagesFolder, `${language}.json`))),
    );
    return errorCodes
      .filter((code) => !keys.has(`errors.${code}`))
      .map((code) => `${language}.json: no key errors.${code}`);
  });
}

const dateFormatting =
  /\.toLocale(?:Date|Time)?String\(|\bIntl\.(?:DateTimeFormat|RelativeTimeFormat)\b/g;

/** Rule F18: only `format-date-time.ts` formats a date or a time. */
export function dateFormattingOutsideFormatter(
  root: string,
  options: Options = {},
): Finding[] {
  return filesUnder(root)
    .filter(
      (path) =>
        isCode(path) &&
        !isTest(path) &&
        !isGenerated(path) &&
        basename(path) !== "format-date-time.ts" &&
        !ignored(root, path, options),
    )
    .flatMap((path) =>
      linesMatching(
        root,
        path,
        dateFormatting,
        (match) => `"${match}" formats a date; call format-date-time.ts`,
      ),
    );
}

/**
 * Rule F3: no value of a contract enum as a string literal. A screen reads `Object.values(Language)` or
 * `Language.En`. A literal that only starts with a value, such as the message key `errors.tenant.not_found`, is allowed, and so
 * is a literal type, such as the element name of `ComponentProps<"tr">`.
 */
export function enumLiterals(
  root: string,
  values: ReadonlySet<string>,
  options: Options = {},
): Finding[] {
  const findings: Finding[] = [];
  for (const path of filesUnder(root)) {
    if (
      !isCode(path) ||
      isTest(path) ||
      isGenerated(path) ||
      ignored(root, path, options)
    )
      continue;
    const file = sourceFile(path);
    const modules = new Set(importedModules(file));
    const visit = (node: ts.Node) => {
      if (
        (ts.isStringLiteral(node) ||
          ts.isNoSubstitutionTemplateLiteral(node)) &&
        !modules.has(node as ts.StringLiteral) &&
        !ts.isLiteralTypeNode(node.parent) &&
        values.has(node.text)
      ) {
        findings.push(
          `${relative(root, path)}:${lineOf(file, node)}: enum value "${node.text}" as a literal`,
        );
      }
      ts.forEachChild(node, visit);
    };
    visit(file);
  }
  return findings;
}

/**
 * The enums whose values no screen writes by hand (F3): every const object of the generated client but the sort orders
 * (`...Sort`) and the kinds (`...Kind`). Their values are common words of code and HTML (`email`, `name`, `user`, `platform`), so a
 * check would refuse `type="email"`. A screen still writes them through the const object: `ListMembersSort.Email`.
 */
export function contractEnumNames(generatedSchemasFile: string): string[] {
  const text = readFileSync(generatedSchemasFile, "utf8");
  return [...text.matchAll(/^export const (\w+) = \{/gm)]
    .map((match) => match[1] ?? "")
    .filter((name) => !name.endsWith("Sort") && !name.endsWith("Kind"));
}

/** Every value of the contract enums, read from the generated client. */
export function contractEnumValues(generatedSchemasFile: string): Set<string> {
  return new Set(
    contractEnumNames(generatedSchemasFile).flatMap((name) =>
      enumValues(generatedSchemasFile, name),
    ),
  );
}

const comparisons = new Set([
  ts.SyntaxKind.EqualsEqualsEqualsToken,
  ts.SyntaxKind.ExclamationEqualsEqualsToken,
  ts.SyntaxKind.EqualsEqualsToken,
  ts.SyntaxKind.ExclamationEqualsToken,
]);

const roleNameFunctions = new Set(["builtInRoleOf", "roleSelect"]);

/** A role's name: `role.name`, `member.role?.name`, a `BuiltInRole` member, or the answer of `builtInRoleOf(...)` or `roleSelect(...)`. */
function isRoleName(node: ts.Expression, file: ts.SourceFile): boolean {
  const inner = ts.skipPartiallyEmittedExpressions(
    ts.isParenthesizedExpression(node) ? node.expression : node,
  );
  if (ts.isPropertyAccessExpression(inner)) {
    if (inner.name.text === "name")
      return /role/i.test(inner.expression.getText(file));
    return inner.expression.getText(file) === "BuiltInRole";
  }
  return (
    ts.isCallExpression(inner) &&
    roleNameFunctions.has(inner.expression.getText(file))
  );
}

/**
 * Rule F3: a piece of the contract that `packages/api-client` writes has no second copy in an app. A declaration of one of the names
 * (a variable, a function or a type) is a finding: the app imports it from the client. Rule F29 uses it for the parts both apps share:
 * `from` names their package and `rule` the rule.
 */
export function declarationsOf(
  root: string,
  names: readonly string[],
  {
    from = "@repo/api-client",
    rule = "F3",
    ...options
  }: Options & { from?: string; rule?: string } = {},
): Finding[] {
  const findings: Finding[] = [];
  for (const path of filesUnder(root)) {
    if (
      !isCode(path) ||
      isTest(path) ||
      isGenerated(path) ||
      ignored(root, path, options)
    )
      continue;
    const file = sourceFile(path);
    const visit = (node: ts.Node) => {
      if (
        (ts.isVariableDeclaration(node) ||
          ts.isFunctionDeclaration(node) ||
          ts.isTypeAliasDeclaration(node)) &&
        node.name &&
        ts.isIdentifier(node.name) &&
        names.includes(node.name.text)
      )
        findings.push(
          `${relative(root, path)}:${lineOf(file, node)}: declares ${node.name.text}; import it from ${from} (${rule})`,
        );
      ts.forEachChild(node, visit);
    };
    visit(file);
  }
  return findings;
}

/**
 * Rule F51: a role's name decides nothing; the permissions do. A comparison or a `switch` on a role's name is a finding, and so is
 * an import of `builtInRoleOf`. The files that give a role its text and its list order are ignored by the caller.
 */
export function roleNameDecisions(
  root: string,
  options: Options = {},
): Finding[] {
  const findings: Finding[] = [];
  for (const path of filesUnder(root)) {
    if (
      !isCode(path) ||
      isTest(path) ||
      isGenerated(path) ||
      ignored(root, path, options)
    )
      continue;
    const file = sourceFile(path);
    const finding = (node: ts.Node, what: string) =>
      findings.push(`${relative(root, path)}:${lineOf(file, node)}: ${what}`);
    const visit = (node: ts.Node) => {
      if (
        (ts.isBinaryExpression(node) &&
          comparisons.has(node.operatorToken.kind) &&
          (isRoleName(node.left, file) || isRoleName(node.right, file))) ||
        (ts.isSwitchStatement(node) && isRoleName(node.expression, file))
      )
        finding(
          node,
          "compares a role's name; decide by its permissions (F51)",
        );
      if (
        ts.isImportSpecifier(node) &&
        (node.propertyName ?? node.name).text === "builtInRoleOf"
      )
        finding(
          node,
          "imports builtInRoleOf; a role's name is only its text (F51)",
        );
      ts.forEachChild(node, visit);
    };
    visit(file);
  }
  return findings;
}

const choiceTypes = new Set(["checkbox", "radio"]);

/** Rule F52: no native checkbox or radio. A choice is `Checkbox` or `RadioGroup` of `packages/ui`, which follow the tokens. */
export function nativeChoiceControls(
  root: string,
  options: Options = {},
): Finding[] {
  const findings: Finding[] = [];
  for (const path of filesUnder(root)) {
    if (!path.endsWith(".tsx") || isTest(path) || ignored(root, path, options))
      continue;
    const file = sourceFile(path);
    const visit = (node: ts.Node) => {
      if (
        ts.isJsxAttribute(node) &&
        node.name.getText(file) === "type" &&
        node.initializer
      ) {
        const value = ts.isJsxExpression(node.initializer)
          ? node.initializer.expression
          : node.initializer;
        if (
          value &&
          ts.isStringLiteralLike(value) &&
          choiceTypes.has(value.text)
        )
          findings.push(
            `${relative(root, path)}:${lineOf(file, node)}: a native type="${value.text}"; use Checkbox or RadioGroup of @repo/ui (F52)`,
          );
      }
      ts.forEachChild(node, visit);
    };
    visit(file);
  }
  return findings;
}
