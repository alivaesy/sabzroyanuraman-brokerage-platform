import pypandoc, os, textwrap

content = r"""# LOCAL_DEV_SETUP_HOME_PC

## Purpose

This document describes the Home PC development environment used for the project:

sabz-group1/brokerage-platform

The purpose is to help future developers or AI assistants understand the local environment without rediscovering the setup.

---

# Machine Information

## Machine Role

Home PC

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

---

# Docker Environment

## Docker Desktop

Version:

4.89.0

## Docker Engine

Version:

29.7.2

## Configuration

Context:

desktop-linux

Operating System:

Docker Desktop (containerized)

Container Type:

Linux Containers

Backend:

WSL2

---

# Docker Resources

CPU:

2 cores

Memory:

3.793 GiB

---

# GitLab Runner

## Runner Name

brokerage-windows-docker

## Runner Version

18.6.1

## Executor

docker

## Runner Status

Service is running

## GitLab URL

https://gitlab.com

---

# Project Information

Repository:

sabz-group1/brokerage-platform

Local Path:

C:\Users\Asus\brokerage-platform

Current Status:

Local repository exists and is connected to GitLab.

Note:

This local copy must be synchronized with the latest GitLab version before building or deployment testing.

---

# Local Development Workflow

Open project:

```powershell
cd C:\Users\Asus\brokerage-platform