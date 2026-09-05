import { useQuery } from '@tanstack/react-query';
import { getEnrolledStructuredLearningDocuments } from '@/features/capabilities/api/enrolledStructuredLearningApi';
import {
  getStructuredLearningDocument,
  getStructuredLearningDocumentEntities,
  getStructuredLearningDocumentConnections,
  getActiveStructuredLearningAssessmentRound,
  type StructuredLearningDocumentEntity,
  type StructuredLearningConnection,
} from '@/features/capabilities/api/structuredLearningRuntimeApi';
import type { SelectedNeuron } from './types';

export interface MasteredEngramItem {
  sectionId: string;
  documentId: string;
  documentName: string;
  title: string;
  completedDate?: string;
  reviewCount: number;
  finalScore?: number;
  neuron: SelectedNeuron;
}

export interface BridgeConceptItem {
  id: string;
  name: string;
  canonicalName: string;
  entityType: string;
  ontologicalClass?: string | null;
  /** Already-precomputed, extraction-time canonical definition (e.g. "what
   * is HTTP") — from StructuredLearningEntity.Description, never generated
   * live. Null if the extraction agent didn't produce one for this entity. */
  description?: string | null;
  connectedEngrams: MasteredEngramItem[];
  connections: {
    relationshipLabel: string;
    targetEngramId: string;
    targetEngramTitle: string;
    sourceSectionId?: string | null;
    /** Verbatim quoted source text this specific connection was extracted
     * from — also already precomputed, no agent call needed to show it. */
    evidence?: string | null;
  }[];
}

export interface KnowledgeNetworkData {
  masteredEngrams: MasteredEngramItem[];
  bridgeConcepts: BridgeConceptItem[];
  rawEntities: StructuredLearningDocumentEntity[];
  rawConnections: StructuredLearningConnection[];
  availableCourses: { id: string; name: string }[];
}

export function useKnowledgeNetwork(personId: string, courseId?: string | null) {
  return useQuery<KnowledgeNetworkData>({
    queryKey: ['knowledgeNetwork', personId, courseId ?? 'all'],
    queryFn: async () => {
      if (!personId) {
        return { masteredEngrams: [], bridgeConcepts: [], rawEntities: [], rawConnections: [], availableCourses: [] };
      }

      // 1. Fetch enrolled structured learning courses
      const allDocs = await getEnrolledStructuredLearningDocuments(personId).catch(() => []);
      const availableCourses = allDocs.map((d) => ({
        id: d.structuredLearningDocumentId,
        name: d.documentName,
      }));

      const docs = courseId
        ? allDocs.filter((d) => d.structuredLearningDocumentId === courseId)
        : allDocs;

      const courseDetails = await Promise.all(
        docs.map(async (d) => {
          try {
            const [docDetail, entities, connections] = await Promise.all([
              getStructuredLearningDocument(d.structuredLearningDocumentId),
              getStructuredLearningDocumentEntities(d.structuredLearningDocumentId).catch(() => []),
              getStructuredLearningDocumentConnections(d.structuredLearningDocumentId).catch(() => []),
            ]);
            return { doc: d, docDetail, entities, connections };
          } catch {
            return null;
          }
        })
      );

      const masteredEngrams: MasteredEngramItem[] = [];
      const allEntities: StructuredLearningDocumentEntity[] = [];
      const allConnections: StructuredLearningConnection[] = [];

      for (const item of courseDetails) {
        if (!item) continue;
        const { doc, docDetail, entities, connections } = item;
        allEntities.push(...entities);
        allConnections.push(...connections);

        const sectionRounds = await Promise.all(
          docDetail.Sections.map(async (sec) => {
            const round = await getActiveStructuredLearningAssessmentRound(
              sec.StructuredLearningDocumentSectionId,
              personId
            ).catch(() => null);
            return { sec, round };
          })
        );

        for (const { sec, round } of sectionRounds) {
          if (round?.Status === 'Passed') {
            masteredEngrams.push({
              sectionId: sec.StructuredLearningDocumentSectionId,
              documentId: doc.structuredLearningDocumentId,
              documentName: doc.documentName,
              title: sec.SubchapterTitle || sec.ChapterTitle,
              completedDate: round.CompletedDate ?? undefined,
              reviewCount: round.RoundNumber ?? 1,
              finalScore: round.FinalScore ?? undefined,
              neuron: {
                capabilityGraphNodeId: sec.StructuredLearningDocumentSectionId,
                name: sec.SubchapterTitle || sec.ChapterTitle,
                state: 'Mastered',
                lastCompletedDate: round.CompletedDate ?? undefined,
                reviewCount: round.RoundNumber ?? 1,
                finalScore: round.FinalScore ?? undefined,
                capabilityId: doc.structuredLearningDocumentId,
                capabilityName: doc.documentName,
                isStructuredLearning: true,
                structuredLearningDocumentId: doc.structuredLearningDocumentId,
                structuredLearningSectionId: sec.StructuredLearningDocumentSectionId,
              },
            });
          }
        }
      }

      const masteredSectionIds = new Set(masteredEngrams.map((m) => m.sectionId));
      const engramMap = new Map<string, MasteredEngramItem>(masteredEngrams.map((m) => [m.sectionId, m]));

      // 2. Group entities by normalized name to find Bridge Concepts across mastered engrams
      const byNormName = new Map<
        string,
        {
          canonicalName: string;
          entityType: string;
          ontologicalClass?: string | null;
          matchingEntities: StructuredLearningDocumentEntity[];
          masteredEngrams: Set<string>;
        }
      >();

      for (const ent of allEntities) {
        const norm = ent.CanonicalName.trim().toLowerCase();
        if (!norm) continue;

        // Count occurrences within MASTERED engrams
        const inMastered = ent.Occurrences
          .map((o) => o.EngramId)
          .filter((id) => masteredSectionIds.has(id));

        if (inMastered.length === 0) continue;

        const existing = byNormName.get(norm) ?? {
          canonicalName: ent.CanonicalName.trim(),
          entityType: ent.EntityType,
          ontologicalClass: ent.OntologicalClass,
          matchingEntities: [],
          masteredEngrams: new Set<string>(),
        };

        existing.matchingEntities.push(ent);
        inMastered.forEach((id) => existing.masteredEngrams.add(id));
        byNormName.set(norm, existing);
      }

      // 3. Filter strictly: ONLY entities present in 2+ MASTERED engrams
      const bridgeConcepts: BridgeConceptItem[] = [];

      for (const [norm, group] of byNormName.entries()) {
        if (group.masteredEngrams.size >= 2) {
          const connectedEngramList = Array.from(group.masteredEngrams)
            .map((id) => engramMap.get(id)!)
            .filter(Boolean);

          const allEntIds = new Set(group.matchingEntities.map((e) => e.EntityId));

          // Collect real relationship labels connecting to this bridge concept
          const relatedConns = allConnections.filter(
            (c) =>
              c.SourceSectionId &&
              group.masteredEngrams.has(c.SourceSectionId) &&
              (allEntIds.has(c.SourceEntityId) || allEntIds.has(c.TargetEntityId))
          );

          const connectionInfoList = relatedConns.map((c) => {
            const engram = engramMap.get(c.SourceSectionId!)!;
            return {
              relationshipLabel: c.RelationshipLabel,
              targetEngramId: c.SourceSectionId!,
              targetEngramTitle: engram?.title ?? 'Engram',
              sourceSectionId: c.SourceSectionId,
              evidence: c.Evidence,
            };
          });

          // Already-precomputed canonical definition (extraction-time, never
          // generated live) — first non-empty Description among the matched
          // entity rows sharing this normalized name.
          const description = group.matchingEntities.find((e) => e.Description)?.Description ?? null;

          bridgeConcepts.push({
            id: `concept-${norm}`,
            name: group.canonicalName,
            canonicalName: group.canonicalName,
            entityType: group.entityType,
            ontologicalClass: group.ontologicalClass,
            description,
            connectedEngrams: connectedEngramList,
            connections: connectionInfoList,
          });
        }
      }

      return {
        masteredEngrams,
        bridgeConcepts,
        rawEntities: allEntities,
        rawConnections: allConnections,
        availableCourses,
      };
    },
    staleTime: 60_000,
  });
}
