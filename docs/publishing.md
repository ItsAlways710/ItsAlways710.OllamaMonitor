# Publishing ElBruno.OllamaMonitor to NuGet

This guide covers how the `ElBruno.OllamaMonitor` global tool package is published using **OIDC Trusted Publishing**, following the same release model used in `ElBruno.LocalLLMs`.

## Package

| Package | Project | NuGet |
|---------|---------|-------|
| `ElBruno.OllamaMonitor` | `src/ItsAlways710.OllamaMonitor.Tool/ItsAlways710.OllamaMonitor.Tool.csproj` | [nuget.org/packages/ElBruno.OllamaMonitor](https://www.nuget.org/packages/ElBruno.OllamaMonitor) |

---

## One-Time Setup

### 1. NuGet.org — Add Trusted Publishing Policy

1. Go to **nuget.org → Manage Packages → ElBruno.OllamaMonitor → Trusted publishers**
2. Click **Add trusted publisher** and fill in:

   | Field | Value |
   |-------|-------|
   | Repository owner | `ItsAlways710` |
   | Repository name | `ItsAlways710.OllamaMonitor` |
   | Workflow file | `publish.yml` |
   | Environment | `release` |

3. Save the policy.

### 2. GitHub — Create `release` Environment

1. Go to **Settings → Environments → New environment**
2. Name it: `release`
3. Optionally add reviewers or branch restrictions

### 3. GitHub — Add `NUGET_USER` Secret

1. Go to **Settings → Secrets and variables → Actions**
2. Add a repository secret:

   | Name | Value |
   |------|-------|
   | `NUGET_USER` | Your NuGet.org profile username |

> No `NUGET_API_KEY` secret is required. The workflow exchanges GitHub OIDC identity for a short-lived NuGet API key.

---

## How to Publish

### Option A: GitHub Release

1. Update the package version in the tool project or publish with a release tag
2. Push changes to `main`
3. Create a GitHub Release with a tag like `v0.5.0`
4. The release event triggers `publish.yml`
5. The workflow builds, packages, and pushes the `.nupkg` to NuGet.org

### Option B: Manual Dispatch

1. Open **Actions → Publish to NuGet**
2. Click **Run workflow**
3. Optionally provide a package version
4. If no version is provided, the workflow falls back to the version in the tool project

---

## How It Works

The publish workflow follows the same pattern as `ElBruno.LocalLLMs`, adapted for this Windows desktop tool:

1. Checkout the repository
2. Setup .NET 10
3. Resolve the version from release tag, manual input, or csproj
4. Restore and build the solution
5. Run `build/Pack-Tool.ps1` to package the global tool and inject the WPF desktop payload
6. Exchange GitHub OIDC identity for a temporary NuGet API key via `NuGet/login@v1`
7. Push the package to NuGet.org
8. Upload the produced `.nupkg` as a workflow artifact

---

## Version Resolution

The workflow uses this priority order:

| Priority | Source | When |
|----------|--------|------|
| 1 | Release tag | `release` event — `v0.5.0` becomes `0.5.0` |
| 2 | Manual input | `workflow_dispatch` with `version` specified |
| 3 | csproj fallback | Reads `<Version>` from `src/ItsAlways710.OllamaMonitor.Tool/ItsAlways710.OllamaMonitor.Tool.csproj` |

Versions are validated against `^[0-9]+\.[0-9]+\.[0-9]+`.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `403 Forbidden` on NuGet push | Trusted Publishing policy mismatch | Verify repo owner, repo name, workflow file, and environment match exactly |
| `OIDC token exchange failed` | Missing `id-token: write` permission | Ensure the publish job grants `id-token: write` |
| `NUGET_USER` missing | Repository secret not configured | Add the `NUGET_USER` secret |
| Publish workflow does not run | Release not published | Use a published GitHub Release, not only a tag |
| Package builds locally but not in CI | Windows-specific packaging step | Ensure the workflow runs on `windows-latest` |

---

## References

- [NuGet Trusted Publishing](https://devblogs.microsoft.com/nuget/trusted-publishing-for-nuget-packages/)
- [NuGet/login GitHub Action](https://github.com/NuGet/login)
- [GitHub OIDC](https://docs.github.com/en/actions/security-for-github-actions/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
