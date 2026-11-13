import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'
import llmstxt from 'vitepress-plugin-llms'
import { readdir, readFile, writeFile } from 'node:fs/promises'
import { join } from 'node:path'

const repository = 'https://github.com/weboost-at/Typeshift'
const site = 'https://weboost-at.github.io'
const description = 'Code-defined agents and toolsets for .NET on Microsoft.Extensions.AI'

// GitHub Pages serves a project site under the repository's name; the Pages workflow passes the real base
const base = process.env.DOCS_BASE ?? '/Typeshift/'

// The sidebar order is the reading order: the Introduction first, which takes a reader from nothing to an app that
// uses the library, then building agents, running them, their settings and storage, production, the concepts that
// explain the pages before them, and the reference.
// The Introduction has no `collapsed` key, so it is always open; the others start collapsed, and VitePress opens one
// when it holds the current page.
// docs/.vitepress/lint.mjs fails on a page the sidebar leaves out.
const sidebar = {
  '/guide/': [
    {
      text: 'Introduction',
      items: [
        { text: 'What is Typeshift?', link: '/guide/what-is-typeshift' },
        { text: 'Getting started', link: '/guide/getting-started' },
        { text: 'Using an agent', link: '/guide/using-an-agent' },
        { text: 'Registration', link: '/guide/registration' }
      ]
    },
    {
      text: 'Building agents',
      collapsed: true,
      items: [
        { text: 'Toolsets', link: '/guide/toolsets' },
        { text: 'Tool settings', link: '/guide/tool-settings' },
        { text: 'Agents', link: '/guide/defining-agents' },
        { text: 'Models and providers', link: '/guide/models' },
        { text: 'MCP servers', link: '/guide/mcp' },
        { text: 'Sub-agents and handoffs', link: '/guide/multi-agent' }
      ]
    },
    {
      text: 'Running agents',
      collapsed: true,
      items: [
        { text: 'Running an agent', link: '/guide/running-agents' },
        { text: 'Conversations', link: '/guide/conversations' },
        { text: 'Approval and modes', link: '/guide/tool-approval' },
        { text: 'Middleware and context', link: '/guide/middleware' },
        { text: 'Serving a chat endpoint', link: '/guide/chat-endpoint' }
      ]
    },
    {
      text: 'Settings and storage',
      collapsed: true,
      items: [
        { text: 'Settings and stores', link: '/guide/settings-and-stores' },
        { text: 'Implementing a store', link: '/guide/implementing-a-store' }
      ]
    },
    {
      text: 'Production',
      collapsed: true,
      items: [
        { text: 'Observability', link: '/guide/observability' },
        { text: 'Native AOT', link: '/guide/native-aot' }
      ]
    },
    {
      text: 'Concepts',
      collapsed: true,
      items: [
        { text: 'How Typeshift reads your code', link: '/guide/how-typeshift-reads-your-code' },
        { text: 'How a run works', link: '/guide/how-a-run-works' }
      ]
    },
    {
      text: 'Reference',
      collapsed: true,
      items: [
        { text: 'Builders', link: '/guide/builders' },
        { text: 'The AddTypeshift() chain', link: '/guide/registration#the-chain' },
        { text: 'AgentHost and generated members', link: '/guide/generated-members' },
        { text: 'Diagnostics', link: '/guide/diagnostics' },
        { text: 'Glossary', link: '/guide/glossary' }
      ]
    }
  ],
  '/contributing/': [
    {
      text: 'Contributing',
      items: [
        { text: 'Overview', link: '/contributing/' },
        { text: 'Architecture', link: '/contributing/architecture' },
        { text: 'Decisions', link: '/contributing/decisions' },
        { text: 'Status', link: '/contributing/status' },
        { text: 'Releasing', link: '/contributing/releasing' },
        { text: 'Writing the docs', link: '/contributing/writing' }
      ]
    }
  ]
}

export default withMermaid(
  defineConfig({
    title: 'Typeshift',
    description,
    base,
    // VitePress leaves head links alone, so they carry the base themselves
    head: [
      ['link', { rel: 'icon', type: 'image/svg+xml', href: `${base}brand/favicon.svg` }],
      ['link', { rel: 'icon', type: 'image/png', sizes: '32x32', href: `${base}brand/favicon-32.png` }],
      ['link', { rel: 'apple-touch-icon', href: `${base}brand/apple-touch-icon.png` }]
    ],
    cleanUrls: true,
    // The Markdown copies keep the text of mdsnippets' source links. A page outside the guide has no copy, so a link
    // to one goes to its page of the site, and llms-full.txt sits above the guide that its pages' relative links
    // point into
    async buildEnd({ outDir }) {
      for (const file of await readdir(outDir, { recursive: true })) {
        if (!/\.(md|txt)$/.test(file)) continue

        const path = join(outDir, file)
        let text = (await readFile(path, 'utf8'))
          .replaceAll('\n\nsnippet source | anchor\n', '\n')
          .replace(/\]\(\.\.\/([^)#\s]*?)(?:index)?\.md(#[^)\s]*)?\)/g, `](${site}${base}$1$2)`)
        if (file === 'llms-full.txt') text = text.replaceAll('](./', `](${site}${base}guide/`)

        if (text.includes('](../')) throw new Error(`${file} still links out of the guide by a relative path`)

        await writeFile(path, text)
      }
    },
    lastUpdated: true,
    // The page templates are shown on the writing page, never served as pages
    srcExclude: ['**/templates/**'],
    markdown: {
      // mdsnippets names a fence after the file's extension
      languageAlias: { csproj: 'xml' }
    },
    themeConfig: {
      logo: { light: '/brand/mark.svg', dark: '/brand/mark-dark.svg', alt: 'Typeshift' },
      nav: [
        { text: 'Guide', link: '/guide/getting-started', activeMatch: '/guide/' },
        { text: 'Diagnostics', link: '/guide/diagnostics' },
        { text: 'Contributing', link: '/contributing/', activeMatch: '/contributing/' },
        { text: 'Changelog', link: `${repository}/blob/main/CHANGELOG.md` },
        // As Marten and Wolverine do: the whole guide in one file, for a coding assistant's context
        { text: 'LLM Friendly Docs', link: `${site}${base}llms-full.txt` }
      ],
      sidebar,
      socialLinks: [{ icon: 'github', link: repository }],
      editLink: {
        pattern: `${repository}/edit/main/docs/:path`,
        text: 'Edit this page on GitHub'
      },
      // The contributing pages are for this repository: they stay searchable, ranked below the guide
      search: {
        provider: 'local',
        options: {
          miniSearch: {
            searchOptions: {
              boostDocument: id => (String(id).includes('/contributing/') ? 0.2 : 1)
            }
          }
        }
      },
      outline: { level: [2, 3], label: 'On this page' },
      footer: {
        message: 'Released under the MIT License.'
      }
    },
    mermaid: {
      theme: 'neutral',
      look: 'handDrawn'
    },
    // Mermaid alone is a chunk of several megabytes, loaded only by pages with a diagram
    vite: {
      build: { chunkSizeWarningLimit: 4000 },
      // llms.txt, llms-full.txt and a .md copy of each guide page, for the coding assistants of the library's users;
      // the contributing pages are for this repository
      plugins: [
        llmstxt({
          domain: site,
          description,
          sidebar: { '/guide/': sidebar['/guide/'] },
          ignoreFiles: ['contributing/**']
        })
      ]
    }
  })
)
