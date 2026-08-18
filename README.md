# GYM — Project Documentation
### Multi-branch gym management + showcase web application

A complete design-and-build documentation set, researched against cult.fit, Equinox, Barry's, Third Space, Gymshark, Peloton, Nike, David Lloyd, and the leading gym-management platforms (Mindbody, Glofox, Wodify, PushPress, Zen Planner, GymMaster, Virtuagym).

| File | What it is |
|---|---|
| [01_Research_References.md](01_Research_References.md) | Raw research: cult.fit teardown, design techniques stolen from better sites, 2025-26 trends, 4K image sources, full feature-market scan, rare differentiators, India-context essentials |
| [02_Feature_Specification.md](02_Feature_Specification.md) | The build scope: public website, admin panel (CMS + operations), member portal, 8 signature differentiators, 3 build phases, non-functional requirements |
| [03_Design_System.md](03_Design_System.md) | The visual language: "Dark Luxe Performance" — palette, two-font system, photography grading, motion design, component specs, SVG rules, the "doesn't look AI-built" checklist |
| [04_Architecture_Setup.md](04_Architecture_Setup.md) | React + Vite + Tailwind v4 / .NET 8 / SQL Server architecture, database schema, CMS design, auth model, install & scaffold commands |
| [05_MASTER_PROMPT.md](05_MASTER_PROMPT.md) | **Start here to build.** The ready-to-paste Claude Code prompts (Phase 0–4) + the token-efficiency strategy |

**Quick start:** open Claude Code in this folder → paste **Prompt 0** from `05_MASTER_PROMPT.md` → follow with Prompts 1–4, one fresh session each.

**v1 logins:** one seeded **Admin** (owner — manages everything incl. all website content via CMS) and open **Member** registration (global customer login). Branch-manager/trainer roles are Phase-3 on the same auth system.
