// Bundles entry.js into one ES module under wwwroot/lib/tiptap and collects the licenses of every bundled package.
import { build } from 'esbuild';
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const output = join(root, '../../wwwroot/lib/tiptap');
mkdirSync(output, { recursive: true });

const result = await build({
    entryPoints: [join(root, 'entry.js')],
    outfile: join(output, 'tiptap.js'),
    bundle: true,
    format: 'esm',
    target: 'es2022',
    minify: true,
    legalComments: 'none',
    metafile: true
});

const packages = new Set();
for (const input of Object.keys(result.metafile.inputs)) {
    const match = input.replace(/\\/g, '/').match(/node_modules\/((?:@[^/]+\/)?[^/]+)/);
    if (match) packages.add(match[1]);
}

const sections = [...packages].sort().map(name => {
    const directory = join(root, 'node_modules', name);
    const { version, license } = JSON.parse(readFileSync(join(directory, 'package.json'), 'utf8'));
    const file = readdirSync(directory).find(entry => /^licen[cs]e/i.test(entry));
    const text = file && existsSync(join(directory, file)) ? readFileSync(join(directory, file), 'utf8').trim() : `License: ${license}`;
    return `${name}@${version}\n${'-'.repeat(name.length + version.length + 1)}\n${text}`;
});
writeFileSync(join(output, 'LICENSE.txt'),
    `Third-party licenses for tiptap.js (built from tools/tiptap).\n\n${sections.join('\n\n\n')}\n`);
console.log(`Bundled ${packages.size} packages.`);
