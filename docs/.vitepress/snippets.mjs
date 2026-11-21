// Runs mdsnippets where it cannot cost uncommitted text. The tool rewrites every Markdown file of the repository in
// place and replaces whatever stands between its markers without a word, a draft typed there included.
//
//   npm run docs:snippets        runs `dotnet mdsnippets`. Every Markdown file with uncommitted changes is copied to
//                                .context/mdsnippets-backup/ first, and the uncommitted lines the run replaced are
//                                printed with the snippet they stood in
//   npm run docs:snippets:check  runs the tool on a copy of the repository and fails when a Markdown file of the
//                                tree differs from what the tool writes. It changes no file

import { spawnSync } from 'node:child_process'
import { cpSync, existsSync, mkdtempSync, readFileSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { dirname, join, relative, resolve, sep } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')

// Built from parts, so mdsnippets never takes this file's text for its own markers
const marker = new RegExp('^<!-- ' + 'snippet: (\\S+) -->')
const markerEnd = new RegExp('^<!-- ' + 'endSnippet -->')
const unexpanded = new RegExp('^' + 'snippet: (\\S+)\\s*$')

const run = (command, args) => spawnSync(command, args, { cwd: root, encoding: 'utf8', maxBuffer: 1 << 28 })

const git = (...args) => {
  const result = run('git', args)

  // git diff exits with 1 for a difference
  if (result.error || result.status > (args[0] === 'diff' ? 1 : 0))
    throw new Error(`git ${args[0]} failed: ${result.error?.message ?? result.stderr}`)

  return result.stdout
}

const diff = (...args) => git('diff', '--no-color', '--no-ext-diff', '-U0', ...args)

// What the tool said when it failed, without the list of every snippet it found before that
const mdsnippets = (...args) => {
  const result = run('dotnet', ['mdsnippets', ...args])
  if (result.error) throw new Error(`dotnet did not start: ${result.error.message}`)
  if (result.status === 0) return null

  const output = `${result.stdout}${result.stderr}`.trim()
  return `dotnet mdsnippets failed:\n${output.slice(Math.max(0, output.search(/^Failed/m)))}`
}

const isMarkdown = file => file.toLowerCase().endsWith('.md')
const linesOf = file => readFileSync(file, 'utf8').split('\n')

// The line numbers a diff's hunks cover in the file before it ('-') or after it ('+')
const covered = (hunks, side) =>
  [...hunks.matchAll(/^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@/gm)].flatMap(hunk => {
    const [start, count] = side === '-' ? [hunk[1], hunk[2]] : [hunk[3], hunk[4]]
    return Array.from({ length: Number(count ?? 1) }, (_, index) => Number(start) + index)
  })

// The snippet a line stands in, or the one it asks for before the tool has run
const snippetAt = (lines, number) => {
  const asked = lines[number - 1]?.match(unexpanded)
  if (asked) return asked[1]

  for (let index = number - 1; index >= 0; index--) {
    if (marker.test(lines[index])) return lines[index].match(marker)[1]
    if (markerEnd.test(lines[index]) && index < number - 1) return null
  }

  return null
}

// Neighbouring line numbers as one range each
const ranges = numbers =>
  numbers.reduce((all, number) => {
    if (all.at(-1)?.to === number - 1) all.at(-1).to = number
    else all.push({ from: number, to: number })
    return all
  }, [])

const checkSnippets = () => {
  const copy = mkdtempSync(join(tmpdir(), 'typeshift-snippets-'))

  try {
    // Everything git tracks or would add: the pages, the snippets' sources and the tool's configuration
    const files = git('ls-files', '-z', '--cached', '--others', '--exclude-standard')
      .split('\0')
      .filter(file => file && existsSync(join(root, file)))

    for (const file of files) cpSync(join(root, file), join(copy, file))

    const failure = mdsnippets('--target-directory', copy)
    if (failure) throw new Error(failure.replaceAll(copy + sep, ''))

    const stale = files
      .filter(isMarkdown)
      .filter(file => readFileSync(join(root, file), 'utf8') !== readFileSync(join(copy, file), 'utf8'))

    for (const file of stale) {
      const lines = linesOf(join(root, file))
      const named = new Set()

      for (const [, start] of diff('--no-index', '--', join(root, file), join(copy, file)).matchAll(/^@@ -(\d+)/gm)) {
        const line = Number(start) || 1
        const name = snippetAt(lines, line)
        if (named.has(name)) continue

        named.add(name)
        console.error(`  ${file}:${line}: ${name ? `the snippet ${name}` : 'this text'} differs from what mdsnippets writes`)
      }
    }

    if (!stale.length) return console.log('The snippets in the Markdown match their sources')

    console.error(`\n${stale.length} file(s) with a stale snippet. Run \`npm run docs:snippets\` and commit the result.\n`)
    process.exitCode = 1
  } finally {
    rmSync(copy, { recursive: true, force: true })
  }
}

const writeSnippets = () => {
  // With -z a renamed file is followed by its old name as an entry of its own, which has no status before it
  const entries = git('status', '--porcelain', '-z', '--untracked-files=all').split('\0')
  const dirty = entries
    .filter((entry, index) => entry && !/^[RC]/.test(entries[index - 1] ?? ''))
    .map(entry => ({ untracked: entry.startsWith('??'), file: entry.slice(3) }))
    .filter(({ file }) => isMarkdown(file) && existsSync(join(root, file)))

  const stamp = new Date().toISOString().replace(/\.\d+Z$/, '').replaceAll(':', '-')
  const backup = join(root, '.context/mdsnippets-backup', stamp)

  // What is not committed, by line: every line of a new file, and of a changed one the lines HEAD does not have
  for (const entry of dirty) {
    cpSync(join(root, entry.file), join(backup, entry.file))
    entry.uncommitted = new Set(
      entry.untracked
        ? linesOf(join(root, entry.file)).map((_, index) => index + 1)
        : covered(diff('HEAD', '--', entry.file), '+'))
  }

  const failure = mdsnippets()
  let lost = 0

  for (const { file, uncommitted } of dirty) {
    const kept = join(backup, file)
    const before = linesOf(kept)

    // A line that asks for a snippet is replaced by the snippet, which is what the run is for
    const replaced = covered(diff('--no-index', '--', kept, join(root, file)), '-')
      .filter(number => uncommitted.has(number) && !unexpanded.test(before[number - 1]))

    if (!replaced.length) {
      rmSync(kept)
      continue
    }

    lost += replaced.length
    console.log(`\n${file}: the run replaced ${replaced.length} uncommitted line(s). Your text is in ${relative(root, kept)}`)

    for (const { from, to } of ranges(replaced)) {
      const name = snippetAt(before, from)
      console.log(`  ${from === to ? `line ${from}` : `lines ${from} to ${to}`}${name ? `, in the snippet ${name}` : ''}:`)
      for (let number = from; number <= to; number++) console.log(`    ${before[number - 1]}`)
    }
  }

  if (lost) console.log('\nText between the snippet markers is generated: move yours outside them, or into the sample.')
  else rmSync(backup, { recursive: true, force: true })

  if (failure) throw new Error(failure)
  if (!lost) console.log(`mdsnippets ran${dirty.length ? `, and replaced no uncommitted line of ${dirty.length} changed Markdown file(s)` : ''}`)
}

try {
  if (process.argv.includes('--check')) checkSnippets()
  else writeSnippets()
} catch (error) {
  console.error(error.message)
  process.exitCode = 2
}
