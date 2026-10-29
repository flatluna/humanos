# Session notes — 2026-08-27 (HumanOS backend/Studio)

Safety checkpoint before commit/push. Companion to
`NgoAutomateApp/SESSION_NOTES_2026-08-27.md` (the AI Engram Agents feature
lives mostly in that repo; this repo is the HumanOS side of the same work).

## Changes made this session

- `backend/HumanOS/Services/StructuredLearningKnowledgeRetrievalService.cs`
  — the chat agent was citing far too many Engrams per question. Root
  cause: exact/alias entity matches returned EVERY linked section with no
  relevance ranking (only the semantic *entity* fallback path ranked by
  similarity, not the Engram/section selection itself). Fixed by:
  - Computing the query embedding once, up front, reused for both entity
    matching and Engram ranking.
  - Ranking retrieved/related Engrams by cosine similarity to the actual
    question before capping (`MaxEngramsPerRetrieval = 6`), instead of
    returning them in arbitrary link order.

- `backend/HumanOS/Services/RealtimeVoiceSessionService.cs` — voice agent
  interruption (barge-in) tuning. This is the REAL source of truth for
  `turn_detection` (set at session mint time) — the client's own WebRTC
  `session.update` is redundant/defensive only, so tuning only client-side
  does nothing. History: `threshold: 0.85` (default, never detected
  interruptions) → `0.5` (too sensitive, agent stopped waiting for the
  student to finish a thought) → **currently `0.65`,
  `silence_duration_ms: 650`** as a middle ground. May need further
  real-usage tuning.

- `capabilitystudio/src/pages/AiAgentsChatPage.tsx` — citation modal video
  fixes:
  - Lightbox video had `autoPlay` without `muted`, which browsers silently
    block, leaving it paused on the first frame. Added `muted`/`playsInline`.
  - Added a play-icon overlay on the video thumbnail so it reads as a video,
    not a static image.

- Content fix (not code): the "Dog" Engram in the Silent Language course
  (`df0766b9-7c1f-4e23-a38e-b5cadeda4790`) had 4 generated videos, all
  unpublished. Published the correct final merged 30s video
  (`60ff78a6-1a8c-47e1-8d03-e59e03fe5b1c`) via
  `SetStructuredLearningVideoPublishedFunction` so it shows up for
  volunteers/students. If a course looks like it has "no video," check
  publish state in Studio before assuming a bug.

## Not authored by this session (pre-existing working-tree changes)

`git status` showed several other modified files (video wizard state,
illustration/thumbnail generation, cost tracking, i18n strings, migrations)
that were already in the working tree before this conversation started.
They're included in this safety-checkpoint commit as-is, not reviewed or
authored here.
