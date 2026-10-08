# LOCAL_DEV_SETUP_LAPTOP

## Purpose

This document describes the laptop development environment used for:

` sabz-group1/brokerage-platform `

It is intended to help future developers or AI assistants understand the local environment without rediscovering the setup.

---

# Machine Information

## Machine Role

Laptop

Used for:

- Local development
- Docker testing
- GitLab CI/CD Runner execution
- Build and deployment preparation

---

# Operating System

Product:

Windows 10 Pro

Version:

22H2

OS Build:

19045.6466

Windows Version reported by `Get-ComputerInfo`:

2009

---

# GitLab Runner

## Runner Name

`brokerage-laptop-docker`

## Runner Version

19.4.1

## Executor

`docker`

## Runner Status

Service is running

## GitLab URL

`https://gitlab.com`

## Runner Configuration

`C:\GitLab-Runner\config.toml`

## Runner Installation Directory

`C:\GitLab-Runner`

---

# WSL2

WSL is installed and Docker Desktop is using WSL2.

Current distribution reported:

`docker-desktop`

State at time of documentation:

`Running`

WSL Version:

`2`

---

# Docker Environment

Docker Desktop is installed and integrated with WSL2.

The GitLab Runner uses the Docker executor.

Detailed Docker Desktop / Docker Engine version should be recorded after running:

```powershell
docker version
```

and:

```powershell
docker info
```

---

# Project Information

Repository:

`https://gitlab.com/sabz-group1/brokerage-platform.git`

Local project path:

**Not yet established.**

`C:\GitLab-Runner` is the Runner installation directory, not the Git repository.

Recommended example for the project clone:

```powershell
cd C:\Users\$env:USERNAME
mkdir Projects -ErrorAction SilentlyContinue
cd Projects
git clone https://gitlab.com/sabz-group1/brokerage-platform.git
cd brokerage-platform
```

After cloning, verify:

```powershell
git status
git remote -v
git branch -a
git log -5 --oneline
```

---

# Important Distinction

Do not run Git commands such as:

```powershell
git remote -v
git branch
git status
```

from:

```text
C:\GitLab-Runner
```

unless that directory itself is a Git repository.

`C:\GitLab-Runner` contains the GitLab Runner executable and configuration.

The project repository should be located separately, for example:

```text
C:\Users\<username>\Projects\brokerage-platform
```

---

# Local Development Workflow

After the repository has been cloned:

```powershell
cd C:\Users\<username>\Projects\brokerage-platform
```

Check the working tree:

```powershell
git status
```

Check the remote:

```powershell
git remote -v
```

Check branches:

```powershell
git branch -a
```

Before pulling or changing branches, verify that the working tree is clean and that the intended branch is correct.

---

# Docker Verification

Check Docker version:

```powershell
docker version
```

Check Docker environment:

```powershell
docker info
```

Docker should be running before local container builds or GitLab Runner Docker jobs are tested.

---

# GitLab CI/CD Architecture

```text
GitLab.com
     |
     |
GitLab Runner (Laptop)
     |
     |
Docker Executor
     |
     |
Linux Container
     |
     |
Build / Test
```

The GitLab repository remains the authoritative source of the project.

---

# Project Source of Truth

GitLab is the primary and authoritative repository for the project.

The laptop is a working clone and build/test environment.

The laptop must not become an independent source of truth.

Production deployment remains controlled through the GitLab pipeline.

---

# Deployment Architecture

```text
Developer / ChatGPT
        |
        v
     GitLab
        |
        v
 GitLab Pipeline
        |
        v
 Runner
        |
        v
 Build / Test
        |
        v
 Production VPS / Hosting
```

The laptop Runner is intended primarily for CI/CD execution and local project validation. Production deployment should continue through the established GitLab pipeline.

---

# Runner Maintenance

Check Runner status:

```powershell
cd C:\GitLab-Runner
.\gitlab-runner.exe status
```

List configured runners:

```powershell
.\gitlab-runner.exe list
```

---

# Security Notes

- Never store the GitLab Runner authentication token in this document.
- Never paste the Runner token into project documentation, commits, screenshots, or public messages.
- Because the Runner token was exposed during setup, it should be rotated/reset through GitLab before being treated as a long-term secret.
- Do not unregister or reset the Runner unless the replacement configuration is ready.
- Keep this document updated if the laptop's Docker, WSL, Runner, or project configuration changes.

---

# Pending Information

The following information should be filled after the project is cloned and Docker is checked:

- Local project path
- Docker Desktop version
- Docker Engine version
- Docker context
- Docker operating system / container mode
- Docker CPU and memory allocation
- Current default Git branch
- Confirmation that the local clone matches the latest GitLab commit
- Exact local build command
- Exact test command
- Any project-specific deployment command used by the GitLab pipeline
