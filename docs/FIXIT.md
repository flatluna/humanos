# FIXIT — Production Readiness Backlog

Status snapshot taken 2026-09-24, based on live debugging of the "Grow With Us"
employee app, Human OS Studio, and the backend Azure Functions API. This is not
a wishlist — every item below was either directly observed as a live bug during
this session, or is a structural risk an enterprise buyer's technical due
diligence would flag.

## P0 — Data integrity

- [ ] **Backfill/validate `StructuredLearningAssessmentRound.CompletedDate`.**
      Found 10 rows in dev where `CompletedDate < CreatedDate` (impossible —
      the round claimed to finish before it started), all traced to demo/seed
      data. Add a DB constraint or a nightly integrity job that flags any row
      where `CompletedDate < CreatedDate`, and a migration to backfill from
      `RewardLedgerEntry.EarnedAt` when available.
- [ ] **Add a single source of truth for "last completed date" per engram.**
      Today `GetStructuredLearningSectionSummaryFunction` computes
      `lastCompletedDate` from `MAX(round.CompletedDate, rewardEntry.EarnedAt)`,
      while `useKnowledgeNetwork.ts` (used by the "Stay Fresh" grid and My
      Brain) reads `round.CompletedDate` directly with no reward-ledger
      fallback. These two code paths silently disagree whenever the two dates
      drift, and the UI has no way to detect it. Extract one backend endpoint
      (or one shared computed column) both surfaces call.
- [ ] **Audit all `RewardLedgerEntry.EarnedAt` writes** for the same
      before/after-creation sanity check as above.

## P0 — Voice / Realtime agent config drift

- [ ] **Delete the redundant client-side `session.update` VAD override** in
      all 5 copies (`VoiceTutorAgent.tsx` × capabilitystudio/employee/web,
      `EngramReviewAgent.tsx` × employee/web) instead of just re-syncing the
      threshold value. The backend (`RealtimeVoiceSessionService.cs`) is the
      authoritative source (already correctly tuned, `threshold: 0.65`); the
      client-side "defensive" `session.update` sent on `dataChannel.onopen`
      is proven to silently override it and has already caused one real
      regression (`0.65` → stale `0.85`, breaking interruption/barge-in for
      an unknown period). If the client never needs to change VAD mid-session,
      remove the redundant send entirely so this class of bug can't recur.
      If it does need to send it, load the threshold from config/the mint
      response instead of hardcoding it in 5 places.

## P1 — Frontend duplication (maintenance risk)

- [ ] **Deduplicate `human-os-employee`, `human-os-web`, and
      `capabilitystudio`.** They contain near-identical copies of
      `VoiceTutorAgent.tsx`, `EngramReviewAgent.tsx`, `BrainGraph.tsx`,
      `EngramDetailPanel.tsx`, `StructuredLearningSectionPage.tsx`, and the
      `memoryScience.ts` retention-curve math. Every bug fix today had to be
      applied 2-5 times by hand across copies — this WILL cause silent drift
      (as it already has: see the VAD threshold bug, and the "Employee"
      route mismatch that only existed in `StartInYourRolePage.tsx`, not the
      structurally-identical `InYourRolePage.tsx`). Extract shared logic into
      a common package/workspace, or explicitly document + tooling-enforce
      that these 3 apps must never diverge without a reason.
- [ ] **Add a lint rule or CI check that fails if these known-duplicated
      files diverge** unexpectedly (e.g. a script that diffs them and
      requires an explicit "these are intentionally different" annotation),
      as a stop-gap until the dedup above is done.

## P1 — Routing / navigation correctness

- [ ] Audit every `matchedCapabilityId` + `matchedCourseType` consumer for
      the `/capabilities/{id}` vs `/structured-learning/{id}` routing bug
      class found in `StartInYourRolePage.tsx` (fixed this session) — grep
      for `` `/capabilities/${...}` `` without a preceding `matchedCourseType`
      check and confirm each one is intentional.

## P1 — Content generation fidelity

- [ ] **Verify the new "preserve named entities exactly" prompt rule**
      (added today to `StructuredLearningProfessorLectureAgent.cs` and
      `StructuredLearningWorkplaceApplicationAgent.cs`) actually holds under
      eval, not just spot-check — see Evals section below.
- [ ] **Regenerate previously-published lectures** whose rewritten content
      already dropped named entities present in the source document (found
      at least one: "Get to Know How We Work" → "2 We design the
      interaction" dropped "Employees"). Add a one-time audit script that
      diffs each section's `Text` against its `Teaching.Content` and flags
      entities present in `Text` but absent from `Content`.

## P2 — UX polish found live

- [ ] Entities & Concepts overlay panel in `BrainGraph.tsx` could fully
      obscure the graph canvas on narrower windows (fixed today — now
      collapsible — but confirm the fix ships to `human-os-web`'s graph too
      if/when it gets one).
- [ ] Selecting a concept then switching the course filter left a stale
      concept focus active, making the graph look empty/stuck (fixed today
      via a `courseId`-keyed reset effect) — same class of "filter state
      doesn't reset across dimensions" bug likely exists elsewhere (e.g.
      Status filter vs course filter combinations) — worth a quick pass.

---

## Evals — every agent needs one before next release

None of the agents below currently have an automated eval harness (accuracy/
groundedness/format-compliance scoring against a labeled dataset) — only ad
hoc unit tests of surrounding service code. For a production release, each
agent needs at minimum: a small labeled golden dataset, a scoring rubric, and
a CI-runnable eval script that fails the build below a quality threshold.

### Studio content-generation agents (`Agents/Studio/`)
- [ ] `StructuredLearningProfessorLectureAgent` — eval: entity-preservation
      rate (does the lecture keep every named entity from the source text?
      — the exact bug found this session), TTS-formatting compliance (no
      markdown leakage), language-consistency, groundedness (no invented
      facts).
- [ ] `StructuredLearningWorkplaceApplicationAgent` /
      `StructuredLearningWorkplaceApplicationCqiAgent` /
      `StructuredLearningWorkplaceMentorAgent` — eval: verbatim match between
      `ScenarioDescription` and `SpokenAudioScript` (currently only enforced
      by prompt instruction, never verified), no resume/tool-list recitation,
      entity preservation.
- [ ] `StructuredLearningEntityExtractionAgent` — eval: precision/recall of
      extracted entities against a hand-labeled set of sections, hallucination
      rate (entities with no real `SourceText` match), correct `EntityType`
      classification.
- [ ] `StructuredLearningConnectionExtractionAgent` — eval: relationship
      accuracy between entity pairs.
- [ ] `StructuredLearningKnowledgeFactGenerationAgent` — eval: factual
      groundedness against source entities.
- [ ] `StructuredLearningSectionDesignerAgent` / `StructuredLearningSectionEvaluator`
      / `StructuredLearningStepEditorAgent` / `StructuredLearningExampleCardEditorAgent`
      — eval: pedagogical quality rubric (Bloom's-taxonomy coverage, reading
      level, factual grounding in source chapter).
- [ ] `StructuredLearningAssessmentAgent` / `StructuredLearningRecallAgent` /
      `StructuredLearningDeepDiveAgent` / `StructuredLearningTutorAgent` —
      eval: grading accuracy/consistency (same answer graded the same way
      twice), false-pass / false-fail rate against a labeled answer set.
- [ ] `StructuredLearningVideoPromptAgent` / `FilmSceneContentAgent` /
      `FilmScriptBreakdownAgent` — eval: prompt-to-output relevance, banned
      content checks.
- [ ] `InstructorAgent` / `ArquitectoAgent` / `CuradorAgent` / `ExperienciaAgent`
      / `MetricoAgent` / `ExperienceDesignerAgent` / `GraphArchitectAgent` /
      `GraphNodeAdditionAgent` / `StructuredLearningSectionAdditionAgent` —
      eval: schema/blueprint validity rate (already partially covered by
      `BlueprintValidator`/`ModuleScriptValidator`/`CompletedModuleValidator`
      — promote these from one-off validators into scored eval suites with
      a labeled "known good"/"known bad" corpus).
- [ ] `DocumentContextAgent` / `DocumentTypeOutlineParser` / `TocExtractionAgent`
      / `SyllabusOutlineParser` / `DocumentChapterSplitter` / `PdfImageDescriptionAgent`
      — eval: extraction accuracy against a labeled set of source PDFs.
- [ ] `EngramRelevanceRerankerAgent` — eval: ranking quality (NDCG or similar)
      against labeled relevance judgments.
- [ ] `OutlineTitleRefinementAgent` / `LessonCardAgent` /
      `IdeaToDocumentAgent` — eval: output quality rubric.
- [ ] `TraditionalCourseReviewAgent` / `ChapterExamAgent` — eval: review/exam
      quality and answer-key correctness.
- [ ] `WebGroundingService` — eval: citation accuracy, no fabricated sources.

### Top-level agents (`Agents/`)
- [ ] `SmartObjectiveRecommendationAgent` — eval: recommendation relevance
      against labeled career-plan scenarios.
- [ ] `CareerDevelopmentPlanAgent` / `GrowthPathRecommenderAgent` — eval:
      plan quality/coherence, no hallucinated job roles or skills.
- [ ] `EngramImpactScoringAgent` — eval: score consistency/calibration
      against labeled examples.
- [ ] `ResumeExtractionAgent` / `JobDescriptionExtractionAgent` /
      `JobRoleDescriptionAgent` / `OrgChartExtractionAgent` /
      `CqiReportExtractionAgent` — eval: field-level extraction
      accuracy against labeled source documents (resumes, job postings, org
      charts, CQI reports).

### Runtime agents (`Agents/Runtime/`)
- [ ] `TutorAgent` and other runtime chat/tutoring agents — eval: grounded
      Q&A accuracy, refusal-to-hallucinate rate, tone/persona consistency.

### Realtime voice sessions
- [ ] `RealtimeVoiceSessionService` — this isn't a scorable text agent, but
      still needs a regression eval: an automated check (can be a scripted
      WebRTC test harness) that interruption/barge-in actually works with
      the configured VAD threshold, so the exact bug fixed today (client
      silently reverting the threshold) gets caught by CI next time instead
      of by a live user report.

---

## Suggested next steps

1. Fix the P0 items first (data integrity + VAD config) — both are silent,
   user-invisible-until-reported bugs, the worst kind for a production system.
2. Stand up ONE eval harness pattern (dataset format, scoring script,
   pass/fail threshold, CI hook) and apply it to the 2-3 highest-risk agents
   first (`StructuredLearningEntityExtractionAgent`,
   `StructuredLearningProfessorLectureAgent`,
   `StructuredLearningAssessmentAgent`) before rolling the pattern out to
   the rest of the list.
3. Tackle the frontend dedup as a dedicated refactor sprint, not
   incrementally — it's the biggest structural risk for future bug
   recurrence.
