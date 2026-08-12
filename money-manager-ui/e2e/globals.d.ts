/**
 * The one Node global this suite needs, declared rather than pulled in with `@types/node`.
 *
 * <p>`@types/node` would be the conventional answer, but TypeScript includes every package under
 * `node_modules/@types` in *all* projects that do not pin `types`, so installing it would also
 * make `process`, `Buffer` and friends resolve inside `src/` — where nothing should be reaching
 * for them, and where the compiler currently says so. Fixing that would mean adding `"types": []`
 * to tsconfig.app.json, which is a change to how the shipped bundle is typechecked in service of
 * a test helper.</p>
 *
 * <p>This file is only in tsconfig.node.json's include, so the declaration stops at the tooling
 * project: config files and these specs. `src/` is unaffected.</p>
 */
declare const process: {
  env: Record<string, string | undefined>;
};
