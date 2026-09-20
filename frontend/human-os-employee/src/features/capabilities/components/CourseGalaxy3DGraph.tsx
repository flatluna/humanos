import React, { useMemo, useRef, useEffect, useState, useCallback } from 'react';
import { Link } from 'react-router';
import ForceGraph3D, { type ForceGraphMethods } from 'react-force-graph-3d';
import * as THREE from 'three';
import { useTranslation } from 'react-i18next';
import { Sparkles, Maximize2, Minimize2, RotateCcw, HelpCircle, X, Lock, ListFilter, Plus, Minus } from 'lucide-react';
import { getRetrievability } from '../../brain/memoryScience';
import type {
  StructuredLearningDocumentDetail,
  StructuredLearningDocumentEntity,
  StructuredLearningConnection,
  StructuredLearningSectionDetail,
} from '../api/structuredLearningRuntimeApi';

export interface CourseGalaxy3DGraphProps {
  document: StructuredLearningDocumentDetail;
  entities: StructuredLearningDocumentEntity[];
  connections: StructuredLearningConnection[];
  sectionRounds: Record<string, { status: string; completedDate?: string; finalScore?: number; roundNumber?: number }>;
  onSelectSection?: (section: StructuredLearningSectionDetail) => void;
  height?: number;
}

interface GalaxyNode {
  id: string;
  name: string;
  rawTitle: string;
  nodeType: 'engram' | 'sun' | 'bridge';
  isMastered?: boolean;
  recallPercent?: number | null;
  occurrencesCount?: number;
  entityType?: string;
  color: string;
  size: number;
  section?: StructuredLearningSectionDetail;
  entity?: StructuredLearningDocumentEntity;
  x?: number;
  y?: number;
  z?: number;
}

interface GalaxyLink {
  source: string | GalaxyNode;
  target: string | GalaxyNode;
  label?: string;
  isMasteredPath: boolean;
  confidence?: number | null;
  linkType: 'membership' | 'sequence';
}

export const CourseGalaxy3DGraph: React.FC<CourseGalaxy3DGraphProps> = ({
  document,
  entities,
  sectionRounds,
  onSelectSection,
  height = 540,
}) => {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const fgRef = useRef<ForceGraphMethods | undefined>(undefined);
  const [dimensions, setDimensions] = useState({ width: 800, height });
  const [isFullscreen, setIsFullscreen] = useState(false);
  // Default view stays clean (only concepts shared across 2+ sections) —
  // one-off, single-section concepts are still real data, just hidden
  // behind this toggle instead of cluttering the graph by default.
  const [showAllConcepts, setShowAllConcepts] = useState(false);
  // First-time explainer (what am I looking at / why does it matter) —
  // shown automatically once per browser, reopenable via the "?" button.
  const [showExplainer, setShowExplainer] = useState(() => {
    try {
      return globalThis.localStorage?.getItem('courseGalaxy3d.explainerSeen') !== '1';
    } catch {
      return true;
    }
  });
  const dismissExplainer = useCallback(() => {
    setShowExplainer(false);
    try {
      globalThis.localStorage?.setItem('courseGalaxy3d.explainerSeen', '1');
    } catch {
      // ignore storage errors (e.g. private browsing)
    }
  }, []);
  // Clicked concept (sun/bridge) node — shows which engrams reference it
  // in a modal, turning the abstract line/node graph into a usable index.
  const [selectedEntity, setSelectedEntity] = useState<StructuredLearningDocumentEntity | null>(null);
  // Facet filter — pick an entity from the "Employee (10)" style list to
  // show only the engrams that contain it, like a faceted search filter.
  const [activeFacetEntityId, setActiveFacetEntityId] = useState<string | null>(null);
  const [facetSearch, setFacetSearch] = useState('');
  const [showFacetPanel, setShowFacetPanel] = useState(false);
  // Manual node size control (+/-) — independent of the automatic
  // importance-based sizing, for when the graph feels too big/small.
  const [nodeSizeScale, setNodeSizeScale] = useState(1);

  // Resize observer for responsive canvas
  useEffect(() => {
    if (!containerRef.current) return;
    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const { width, height: h } = entry.contentRect;
        if (width > 0) {
          setDimensions({ width, height: isFullscreen ? h : height });
        }
      }
    });
    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, [height, isFullscreen]);

  // Build filtered Galaxy 3D graph (Sweet spot: ~130 nodes, ~300 links)
  const { graphData, stats, entityNamesByEngramId, facetEntities } = useMemo(() => {
    const nodes: GalaxyNode[] = [];
    const links: GalaxyLink[] = [];
    const nodeIds = new Set<string>();

    const excludeEntityNames = new Set([
      'developer',
      'azure apis + microservices guide',
      'organization',
    ]);

    // 1. Identify Suns (Hyper-Hubs) and Bridges data-driven from each
    // entity's real occurrence count — no hardcoded course-specific names,
    // so this works for any course subject, not just an APIs/microservices one.
    // No minimum-occurrence cutoff here: every non-excluded entity gets a
    // node (even ones that only appear once), just smaller/dimmer — a
    // stricter cutoff used to silently drop those nodes entirely.
    const rawEligibleEntities = entities.filter(
      (ent) => !excludeEntityNames.has(ent.CanonicalName.trim().toLowerCase())
    );

    // The API returns one entity row per section it was extracted in,
    // not one row per unique concept — without merging by name, the same
    // concept (e.g. "Employee") shows up as dozens of near-duplicate
    // entities, which is why counts like 1028 appeared instead of the
    // real, much smaller number of distinct concepts in the course.
    const mergedByName = new Map<string, StructuredLearningDocumentEntity>();
    for (const ent of rawEligibleEntities) {
      const key = ent.CanonicalName.trim().toLowerCase();
      const existing = mergedByName.get(key);
      if (!existing) {
        mergedByName.set(key, { ...ent, Occurrences: [...ent.Occurrences] });
        continue;
      }
      const seenEngramIds = new Set(existing.Occurrences.map((o) => o.EngramId));
      for (const occ of ent.Occurrences) {
        if (!seenEngramIds.has(occ.EngramId)) {
          existing.Occurrences.push(occ);
          seenEngramIds.add(occ.EngramId);
        }
      }
    }
    const eligibleEntities = Array.from(mergedByName.values());
    const sortedByOccurrences = [...eligibleEntities].sort((a, b) => b.Occurrences.length - a.Occurrences.length);
    // Suns = the handful of entities that show up across many sections
    // (top 3, and only if they clearly stand out with 4+ occurrences).
    const sunEntities = sortedByOccurrences.filter((e) => e.Occurrences.length >= 4).slice(0, 3);
    const sunIds = new Set(sunEntities.map((e) => e.EntityId));
    const nonSunEntities = sortedByOccurrences.filter((e) => !sunIds.has(e.EntityId));
    const singleOccurrenceEntities = nonSunEntities.filter((e) => e.Occurrences.length < 2);
    const bridgeEntities = showAllConcepts ? nonSunEntities : nonSunEntities.filter((e) => e.Occurrences.length >= 2);

    // 2. Add Hyper-Hub Suns (Concepts) — blue, bigger than a bridge.
    for (const sun of sunEntities) {
      nodes.push({
        id: sun.EntityId,
        name: sun.CanonicalName,
        rawTitle: sun.CanonicalName,
        nodeType: 'sun',
        color: '#38bdf8', // Sky blue — every concept/entity is blue
        size: 18,
        occurrencesCount: sun.Occurrences.length,
        entityType: sun.EntityType,
        entity: sun,
      });
      nodeIds.add(sun.EntityId);
    }

    // 3. Add Bridge Entities (also blue) — size scales a bit with
    // occurrence count so single-appearance concepts stay visually minor.
    for (const bridge of bridgeEntities) {
      nodes.push({
        id: bridge.EntityId,
        name: bridge.CanonicalName,
        rawTitle: bridge.CanonicalName,
        nodeType: 'bridge',
        color: '#38bdf8', // Sky blue — every concept/entity is blue
        size: bridge.Occurrences.length >= 2 ? 9 : 7,
        occurrencesCount: bridge.Occurrences.length,
        entityType: bridge.EntityType,
        entity: bridge,
      });
      nodeIds.add(bridge.EntityId);
    }

    // 4. Add all 41 Engrams (Course Sections) — gold, and bigger the more
    // concepts they connect to (a real "importance" signal, not decorative).
    const masteredEngramIds = new Set<string>();
    let masteredCount = 0;
    const orderedSections = [...document.Sections].sort((a, b) => a.SortOrder - b.SortOrder);

    const engramEntityCounts = new Map<string, number>();
    // Names of every concept an engram contains — this is the actual
    // "what am I learning here" answer, shown on hover per engram.
    const entityNamesByEngramId = new Map<string, string[]>();
    for (const ent of [...sunEntities, ...bridgeEntities]) {
      for (const occ of ent.Occurrences) {
        engramEntityCounts.set(occ.EngramId, (engramEntityCounts.get(occ.EngramId) ?? 0) + 1);
        const names = entityNamesByEngramId.get(occ.EngramId) ?? [];
        names.push(ent.CanonicalName);
        entityNamesByEngramId.set(occ.EngramId, names);
      }
    }

    for (const sec of orderedSections) {
      const round = sectionRounds[sec.StructuredLearningDocumentSectionId];
      const isMastered = round?.status === 'Passed';
      const reviewCount = round?.roundNumber ?? 1;
      const retrievability = isMastered ? getRetrievability(round.completedDate, reviewCount) : null;
      const recallPercent = retrievability !== null ? Math.round(retrievability * 100) : null;

      if (isMastered) {
        masteredEngramIds.add(sec.StructuredLearningDocumentSectionId);
        masteredCount++;
      }

      // Every engram is gold, except the very first one (the course's
      // starting point), which stands out in red so it's easy to spot.
      const engramColor = sec.SortOrder === 0 ? '#ef4444' : '#fbbf24';

      const connectedEntityCount = engramEntityCounts.get(sec.StructuredLearningDocumentSectionId) ?? 0;
      const baseSize = isMastered ? 20 : 16;
      const size = baseSize + Math.min(connectedEntityCount * 1.5, 12);

      nodes.push({
        id: sec.StructuredLearningDocumentSectionId,
        name: `${sec.SortOrder + 1}. ${sec.SubchapterTitle || sec.ChapterTitle}`,
        rawTitle: sec.SubchapterTitle || sec.ChapterTitle,
        nodeType: 'engram',
        isMastered,
        recallPercent,
        color: engramColor,
        size,
        occurrencesCount: connectedEntityCount,
        section: sec,
      });
      nodeIds.add(sec.StructuredLearningDocumentSectionId);
    }

    // 5. Connect Engrams to their Bridge / Sun Entities — this bipartite
    // structure (engram <-> concept, never concept <-> concept directly)
    // IS the model: a concept "connects" two engrams by being a member of
    // both, so there's no need for separate, harder-to-explain
    // concept-to-concept edges on top of it.
    const existingLinkKeys = new Set<string>();

    const allIncludedEntities = [...sunEntities, ...bridgeEntities];
    for (const ent of allIncludedEntities) {
      for (const occ of ent.Occurrences) {
        if (nodeIds.has(occ.EngramId) && nodeIds.has(ent.EntityId)) {
          const key = `${occ.EngramId}->${ent.EntityId}`;
          if (!existingLinkKeys.has(key)) {
            existingLinkKeys.add(key);
            const isMastered = masteredEngramIds.has(occ.EngramId);
            links.push({
              source: occ.EngramId,
              target: ent.EntityId,
              isMasteredPath: isMastered,
              linkType: 'membership',
            });
          }
        }
      }
    }

    // 7. Always connect consecutive sections in course order — a guaranteed
    // "path" backbone so the graph never renders as disconnected floating
    // dots even when an course has few/no extracted entities or relations.
    for (let i = 0; i < orderedSections.length - 1; i++) {
      const from = orderedSections[i].StructuredLearningDocumentSectionId;
      const to = orderedSections[i + 1].StructuredLearningDocumentSectionId;
      const key = `${from}->${to}`;
      if (!existingLinkKeys.has(key)) {
        existingLinkKeys.add(key);
        links.push({
          source: from,
          target: to,
          isMasteredPath: masteredEngramIds.has(from) && masteredEngramIds.has(to),
          linkType: 'sequence',
        });
      }
    }

    // Facet list — every concept/entity with how many engrams reference it,
    // sorted by count, so the filter panel reads like "Employee (10)".
    const facetEntities = allIncludedEntities
      .map((ent) => ({ id: ent.EntityId, name: ent.CanonicalName, count: ent.Occurrences.length }))
      .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));

    return {
      graphData: { nodes, links },
      entityNamesByEngramId,
      facetEntities,
      stats: {
        totalNodes: nodes.length,
        engramsCount: document.Sections.length,
        masteredCount,
        bridgesCount: bridgeEntities.length,
        sunsCount: sunEntities.length,
        totalLinks: links.length,
        hiddenSingletonCount: showAllConcepts ? 0 : singleOccurrenceEntities.length,
      },
    };
  }, [document, entities, sectionRounds, showAllConcepts]);

  // When a facet is active, only show that concept plus the engrams that
  // reference it (and the course-path links directly between those
  // engrams) — everything else is hidden instead of just dimmed.
  const displayedGraphData = useMemo(() => {
    // `react-force-graph-3d`'s underlying d3-force mutates each link's
    // source/target in place, replacing the string id with the resolved
    // node object once physics starts. If we then hand it a NEW, filtered
    // `nodes` array (as we do below) while reusing those SAME mutated
    // link objects, their source/target still reference the OLD node
    // objects from the unfiltered set — objects no longer present in the
    // new nodes array — so the link silently fails to render. Always
    // normalize back to plain string ids and build fresh link objects.
    const idOf = (endpoint: string | GalaxyNode) => (typeof endpoint === 'object' ? endpoint.id : endpoint);

    if (!activeFacetEntityId) {
      return {
        nodes: graphData.nodes,
        links: graphData.links.map((l) => ({ ...l, source: idOf(l.source), target: idOf(l.target) })),
      };
    }
    const keepNodeIds = new Set<string>([activeFacetEntityId]);
    graphData.links.forEach((l) => {
      const sourceId = idOf(l.source);
      const targetId = idOf(l.target);
      if (l.linkType === 'membership' && (sourceId === activeFacetEntityId || targetId === activeFacetEntityId)) {
        keepNodeIds.add(sourceId);
        keepNodeIds.add(targetId);
      }
    });
    const nodes = graphData.nodes
      .filter((n) => keepNodeIds.has(n.id))
      // Shrink engram nodes while a facet filter is active — with only a
      // handful left on screen, the big "importance size" scaling reads
      // as oversized/cluttered rather than useful.
      .map((n) => (n.nodeType === 'engram' ? { ...n, size: n.size * 0.8 } : n));
    const links = graphData.links
      .filter((l) => {
        const sourceId = idOf(l.source);
        const targetId = idOf(l.target);
        return keepNodeIds.has(sourceId) && keepNodeIds.has(targetId);
      })
      .map((l) => ({ ...l, source: idOf(l.source), target: idOf(l.target) }));
    return { nodes, links };
  }, [graphData, activeFacetEntityId]);

  // Adjust camera & physics on load
  useEffect(() => {
    if (fgRef.current) {
      fgRef.current.d3Force('charge')?.strength(-140);
      fgRef.current.d3Force('link')?.distance(45);
    }
  }, [displayedGraphData]);

  const filteredFacetEntities = useMemo(() => {
    const q = facetSearch.trim().toLowerCase();
    if (!q) return facetEntities;
    return facetEntities.filter((f) => f.name.toLowerCase().includes(q));
  }, [facetEntities, facetSearch]);

  const handleResetCamera = useCallback(() => {
    if (fgRef.current) {
      fgRef.current.cameraPosition({ x: 0, y: 0, z: 280 }, { x: 0, y: 0, z: 0 }, 1000);
    }
  }, []);

  const handleNodeClick = useCallback(
    (node: GalaxyNode) => {
      if (node.nodeType === 'engram' && node.section && onSelectSection) {
        onSelectSection(node.section);
        return;
      }
      if ((node.nodeType === 'sun' || node.nodeType === 'bridge') && node.entity) {
        setSelectedEntity(node.entity);
      }
    },
    [onSelectSection]
  );

  // Custom Three.js rendering for glowing galaxy spheres and suns
  const nodeThreeObject = useCallback((node: GalaxyNode) => {
    const group = new THREE.Group();
    const size = node.size * nodeSizeScale;

    const isSun = node.nodeType === 'sun';
    const isMastered = node.nodeType === 'engram' && node.isMastered;
    const isPendingEngram = node.nodeType === 'engram' && !node.isMastered;
    const isBridge = node.nodeType === 'bridge';

    // Core sphere
    const geometry = new THREE.SphereGeometry(size, isSun ? 24 : 16, isSun ? 24 : 16);
    const material = new THREE.MeshStandardMaterial({
      color: node.color,
      emissive: node.color,
      emissiveIntensity: isSun ? 0.9 : isMastered ? 0.75 : isBridge ? 0.6 : 0.08,
      roughness: isSun ? 0.1 : 0.4,
      metalness: isSun ? 0.1 : 0.3,
      transparent: isPendingEngram,
      opacity: isPendingEngram ? 0.45 : 0.95,
    });
    const mesh = new THREE.Mesh(geometry, material);
    group.add(mesh);

    // Glowing halo aura for Suns and Mastered Engrams
    if (isSun || isMastered) {
      const haloGeo = new THREE.SphereGeometry(size * 1.35, 16, 16);
      const haloMat = new THREE.MeshBasicMaterial({
        color: node.color,
        transparent: true,
        opacity: isSun ? 0.28 : 0.22,
        side: THREE.BackSide,
      });
      group.add(new THREE.Mesh(haloGeo, haloMat));
    }

    // Sequence number badge — every engram shows its order in the course
    // (1, 2, 3...), always visible, not just on hover.
    if (node.nodeType === 'engram' && node.section) {
      // NOTE: `document` here would shadow the DOM global with this
      // component's `document` prop (the course), hence `globalThis.document`.
      const canvas = globalThis.document.createElement('canvas');
      const canvasSize = 64;
      canvas.width = canvasSize;
      canvas.height = canvasSize;
      const ctx = canvas.getContext('2d');
      if (ctx) {
        ctx.clearRect(0, 0, canvasSize, canvasSize);
        ctx.beginPath();
        ctx.arc(canvasSize / 2, canvasSize / 2, canvasSize / 2 - 3, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(15, 23, 42, 0.9)';
        ctx.fill();
        ctx.lineWidth = 3;
        ctx.strokeStyle = node.color;
        ctx.stroke();
        ctx.font = 'bold 30px sans-serif';
        ctx.fillStyle = '#ffffff';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(String(node.section.SortOrder + 1), canvasSize / 2, canvasSize / 2 + 2);

        const texture = new THREE.CanvasTexture(canvas);
        const spriteMat = new THREE.SpriteMaterial({ map: texture, depthTest: false, transparent: true });
        const sprite = new THREE.Sprite(spriteMat);
        const spriteScale = Math.max(7, size * 0.85);
        sprite.scale.set(spriteScale, spriteScale, 1);
        sprite.position.set(0, size + spriteScale * 0.4, 0);
        sprite.renderOrder = 999;
        group.add(sprite);
      }
    }

    return group;
  }, [nodeSizeScale]);

  // Sections (with mastery status) that reference the concept currently
  // selected in the modal — resolved from the entity's own occurrences.
  const selectedEntitySections = useMemo(() => {
    if (!selectedEntity) return [];
    const engramIds = new Set(selectedEntity.Occurrences.map((o) => o.EngramId));
    return document.Sections.filter((sec) => engramIds.has(sec.StructuredLearningDocumentSectionId)).sort(
      (a, b) => a.SortOrder - b.SortOrder
    );
  }, [selectedEntity, document.Sections]);

  // Mirrors the Locked/Available/Mastered gating rule used elsewhere in
  // the course (StructuredLearningCoursePage): a section is only
  // reachable once the previous one in the class path has been passed —
  // the modal shouldn't link to something the student can't open yet.
  const orderedSections = useMemo(() => [...document.Sections].sort((a, b) => a.SortOrder - b.SortOrder), [document.Sections]);
  const isSectionAvailable = useCallback(
    (section: StructuredLearningSectionDetail) => {
      const index = orderedSections.findIndex((s) => s.StructuredLearningDocumentSectionId === section.StructuredLearningDocumentSectionId);
      if (index <= 0) return true;
      const previous = orderedSections[index - 1];
      return sectionRounds[previous.StructuredLearningDocumentSectionId]?.status === 'Passed';
    },
    [orderedSections, sectionRounds]
  );

  return (
    <div
      ref={containerRef}
      className={`relative overflow-hidden rounded-2xl border border-slate-800 bg-[#020617] text-white shadow-2xl transition-all ${
        isFullscreen ? 'fixed inset-0 z-50 rounded-none' : 'w-full'
      }`}
      style={{ height: isFullscreen ? '100vh' : `${height}px` }}
    >
      {/* 3D Force Graph Canvas */}
      <ForceGraph3D
        ref={fgRef as any}
        width={dimensions.width}
        height={dimensions.height}
        graphData={displayedGraphData}
        backgroundColor="#020617"
        showNavInfo={false}
        nodeThreeObject={nodeThreeObject as any}
        nodeLabel={(node: any) => {
          const n = node as GalaxyNode;
          if (n.nodeType === 'engram') {
            const conceptNames = entityNamesByEngramId.get(n.id) ?? [];
            const conceptsHtml = conceptNames.length
              ? `<div style="margin-top: 6px; font-size: 10px; color: #7dd3fc; max-width: 220px;"><span style="color: #38bdf8; font-weight: 600;">${t('courseMap.conceptsInEngram')}</span> ${conceptNames.slice(0, 8).join(', ')}${conceptNames.length > 8 ? '…' : ''}</div>`
              : '';
            return `
              <div style="background: rgba(15, 23, 42, 0.92); border: 1px solid ${n.color}; border-radius: 10px; padding: 8px 12px; font-family: sans-serif; font-size: 12px; color: #fff; box-shadow: 0 4px 20px rgba(0,0,0,0.6); backdrop-filter: blur(8px); max-width: 240px;">
                <div style="font-weight: 700; color: ${n.color}; margin-bottom: 2px;">⚡ ${n.name}</div>
                <div style="font-size: 11px; color: #94a3b8;">${n.isMastered ? `✓ ${t('courseMap.masteredEngram')} (${n.recallPercent}%)` : `○ ${t('courseMap.pendingEngram')}`}</div>
                ${conceptsHtml}
                <div style="margin-top: 4px; font-size: 10px; color: #38bdf8; font-style: italic;">${t('courseMap.clickToInspect')}</div>
              </div>
            `;
          }
          if (n.nodeType === 'sun') {
            return `
              <div style="background: rgba(15, 23, 42, 0.92); border: 1px solid #fbbf24; border-radius: 10px; padding: 8px 12px; font-family: sans-serif; font-size: 12px; color: #fff; box-shadow: 0 4px 20px rgba(251, 191, 36, 0.3);">
                <div style="font-weight: 800; color: #fbbf24; font-size: 13px;">☀️ ${n.name}</div>
                <div style="font-size: 10px; color: #e2e8f0; margin-top: 2px;">${t('courseMap.centralSun')} · ${t('courseMap.occurrences', { count: n.occurrencesCount })}</div>
                <div style="margin-top: 4px; font-size: 10px; color: #fbbf24; font-style: italic;">${t('courseMap.clickToSeeEngrams')}</div>
              </div>
            `;
          }
          return `
            <div style="background: rgba(15, 23, 42, 0.92); border: 1px solid #38bdf8; border-radius: 8px; padding: 6px 10px; font-family: sans-serif; font-size: 11px; color: #fff;">
              <div style="font-weight: 600; color: #38bdf8;">🔗 ${n.name}</div>
              <div style="font-size: 10px; color: #94a3b8;">${n.entityType || 'Concept'} · ${t('courseMap.occurrences', { count: n.occurrencesCount })}</div>
              <div style="margin-top: 4px; font-size: 10px; color: #38bdf8; font-style: italic;">${t('courseMap.clickToSeeEngrams')}</div>
            </div>
          `;
        }}
        linkLabel={(link: any) => {
          const l = link as GalaxyLink;
          if (l.linkType === 'membership') {
            return `<div style="background: rgba(15, 23, 42, 0.95); border: 1px solid #38bdf8; border-radius: 8px; padding: 6px 10px; font-family: sans-serif; font-size: 11px; color: #fff;">${t('courseMap.appearsInThisEngram')}</div>`;
          }
          return `<div style="background: rgba(15, 23, 42, 0.95); border: 1px solid #818cf8; border-radius: 8px; padding: 6px 10px; font-family: sans-serif; font-size: 11px; color: #fff;">${t('courseMap.coursePath')}</div>`;
        }}
        linkColor={(link: any) =>
          link.isMasteredPath
            ? 'rgba(16, 185, 129, 0.9)'
            : link.linkType === 'sequence'
            ? 'rgba(129, 140, 248, 0.55)'
            : 'rgba(56, 189, 248, 0.65)'
        }
        linkOpacity={1}
        linkWidth={(link: any) => (link.isMasteredPath ? 2.2 : link.linkType === 'sequence' ? 1 : 1.2)}
        linkDirectionalParticles={(link: any) => (link.isMasteredPath ? 2 : 0)}
        linkDirectionalParticleSpeed={0.008}
        linkDirectionalParticleWidth={2}
        linkDirectionalParticleColor={() => '#34d399'}
        onNodeClick={handleNodeClick as any}
      />

      {/* Top Header Overlay */}
      <div className="pointer-events-none absolute left-4 top-4 right-4 flex flex-wrap items-center justify-between gap-3">
        <div className="pointer-events-auto flex items-center gap-2.5 rounded-2xl border border-white/10 bg-slate-900/80 px-4 py-2 backdrop-blur-md shadow-lg">
          <Sparkles className="h-4 w-4 text-amber-400" />
          <span className="text-xs font-bold text-white tracking-wide">
            {t('courseMap.legendTitle')}
          </span>
          <span className="inline-flex items-center gap-1 rounded-full bg-emerald-500/20 border border-emerald-500/40 px-2 py-0.5 text-[10px] font-bold text-emerald-300">
            <span className="h-1.5 w-1.5 rounded-full bg-emerald-400 animate-pulse" />
            {t('courseMap.schemaActive', {
              mastered: stats.masteredCount,
              total: stats.engramsCount,
            })}
          </span>
        </div>

        {/* Controls */}
        <div className="pointer-events-auto flex items-center gap-2">
          <div className="flex items-center gap-0.5 rounded-xl border border-white/10 bg-slate-900/80 px-1 py-1 backdrop-blur-md shadow-md">
            <button
              type="button"
              onClick={() => setNodeSizeScale((v) => Math.max(0.4, +(v - 0.15).toFixed(2)))}
              disabled={nodeSizeScale <= 0.4}
              className="flex h-6 w-6 items-center justify-center rounded-lg text-slate-300 hover:bg-slate-800 hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
              title={t('courseMap.shrinkNodes')}
              aria-label={t('courseMap.shrinkNodes')}
            >
              <Minus className="h-3.5 w-3.5" />
            </button>
            <button
              type="button"
              onClick={() => setNodeSizeScale((v) => Math.min(2, +(v + 0.15).toFixed(2)))}
              disabled={nodeSizeScale >= 2}
              className="flex h-6 w-6 items-center justify-center rounded-lg text-slate-300 hover:bg-slate-800 hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
              title={t('courseMap.growNodes')}
              aria-label={t('courseMap.growNodes')}
            >
              <Plus className="h-3.5 w-3.5" />
            </button>
          </div>
          <button
            type="button"
            onClick={() => setShowFacetPanel((v) => !v)}
            className={`flex items-center gap-1 rounded-xl border px-3 py-1.5 text-xs font-semibold backdrop-blur-md shadow-md transition ${
              activeFacetEntityId || showFacetPanel
                ? 'border-emerald-400/50 bg-emerald-500/20 text-emerald-200 hover:bg-emerald-500/30'
                : 'border-white/10 bg-slate-900/80 text-slate-300 hover:bg-slate-800 hover:text-white'
            }`}
            title={t('courseMap.filterByConcept')}
          >
            <ListFilter className="h-3.5 w-3.5" />
            <span className="hidden sm:inline">{t('courseMap.filterByConcept')}</span>
          </button>
          <button
            type="button"
            onClick={() => setShowExplainer(true)}
            className="flex items-center gap-1 rounded-xl border border-white/10 bg-slate-900/80 p-2 text-slate-300 hover:bg-slate-800 hover:text-white backdrop-blur-md shadow-md transition"
            title={t('courseMap.whatIsThis')}
            aria-label={t('courseMap.whatIsThis')}
          >
            <HelpCircle className="h-4 w-4" />
          </button>
          <button
            type="button"
            onClick={() => setShowAllConcepts((v) => !v)}
            className={`flex items-center gap-1 rounded-xl border px-3 py-1.5 text-xs font-semibold backdrop-blur-md shadow-md transition ${
              showAllConcepts
                ? 'border-sky-400/50 bg-sky-500/20 text-sky-200 hover:bg-sky-500/30'
                : 'border-white/10 bg-slate-900/80 text-slate-300 hover:bg-slate-800 hover:text-white'
            }`}
            title={
              showAllConcepts
                ? t('courseMap.hideSingleConcepts')
                : t('courseMap.showAllConceptsHint', { count: stats.hiddenSingletonCount })
            }
          >
            <span className="hidden sm:inline">
              {showAllConcepts
                ? t('courseMap.hideSingleConcepts')
                : t('courseMap.showAllConcepts', { count: stats.hiddenSingletonCount })}
            </span>
            <span className="sm:hidden">{t('courseMap.showAllConceptsShort')}</span>
          </button>
          <button
            type="button"
            onClick={handleResetCamera}
            className="flex items-center gap-1 rounded-xl border border-white/10 bg-slate-900/80 px-3 py-1.5 text-xs font-semibold text-slate-300 hover:bg-slate-800 hover:text-white backdrop-blur-md shadow-md transition"
            title={t('courseMap.resetCamera')}
          >
            <RotateCcw className="h-3.5 w-3.5 text-indigo-400" />
            <span className="hidden sm:inline">{t('courseMap.resetCamera')}</span>
          </button>
          <button
            type="button"
            onClick={() => setIsFullscreen(!isFullscreen)}
            className="flex items-center gap-1 rounded-xl border border-white/10 bg-slate-900/80 p-2 text-slate-300 hover:bg-slate-800 hover:text-white backdrop-blur-md shadow-md transition"
            aria-label={isFullscreen ? 'Exit fullscreen' : 'Fullscreen'}
          >
            {isFullscreen ? <Minimize2 className="h-4 w-4" /> : <Maximize2 className="h-4 w-4" />}
          </button>
        </div>
      </div>

      {/* Facet filter panel — pick a concept ("Employee (10)") to show only
          the engrams that reference it. */}
      {showFacetPanel && (
        <div className="pointer-events-auto absolute left-4 top-16 z-10 w-64 max-h-[70%] overflow-hidden rounded-2xl border border-white/10 bg-slate-900/95 shadow-2xl backdrop-blur-md flex flex-col">
          <div className="flex items-center justify-between gap-2 border-b border-white/10 px-3 py-2.5">
            <p className="text-xs font-bold text-white">{t('courseMap.filterByConcept')}</p>
            <button
              type="button"
              onClick={() => setShowFacetPanel(false)}
              className="rounded-lg p-1 text-slate-400 hover:bg-white/10 hover:text-white"
              aria-label={t('knowledgeGraph.close')}
            >
              <X className="h-3.5 w-3.5" />
            </button>
          </div>
          <div className="border-b border-white/10 p-2">
            <input
              type="text"
              value={facetSearch}
              onChange={(e) => setFacetSearch(e.target.value)}
              placeholder={t('courseMap.searchConcepts')}
              className="w-full rounded-lg border border-white/10 bg-white/[0.04] px-2.5 py-1.5 text-xs text-white placeholder:text-slate-500 focus:border-sky-400/60 focus:outline-none"
            />
          </div>
          {activeFacetEntityId && (
            <button
              type="button"
              onClick={() => setActiveFacetEntityId(null)}
              className="m-2 flex shrink-0 items-center justify-center gap-1.5 rounded-lg border border-emerald-400/40 bg-emerald-500/10 px-2.5 py-1.5 text-xs font-semibold text-emerald-200 hover:bg-emerald-500/20"
            >
              <X className="h-3 w-3" />
              {t('courseMap.clearFilter')}
            </button>
          )}
          <ul className="flex-1 space-y-1 overflow-y-auto p-2">
            {filteredFacetEntities.map((f) => (
              <li key={f.id}>
                <button
                  type="button"
                  onClick={() => setActiveFacetEntityId((prev) => (prev === f.id ? null : f.id))}
                  className={`flex w-full items-center justify-between gap-2 rounded-lg px-2.5 py-1.5 text-left text-xs transition ${
                    activeFacetEntityId === f.id
                      ? 'bg-sky-500/20 text-sky-100 border border-sky-400/50'
                      : 'text-slate-300 border border-transparent hover:bg-white/[0.06] hover:text-white'
                  }`}
                >
                  <span className="min-w-0 flex-1 truncate">{f.name}</span>
                  <span className="shrink-0 text-slate-400">({f.count})</span>
                </button>
              </li>
            ))}
            {filteredFacetEntities.length === 0 && (
              <li className="px-2.5 py-2 text-xs text-slate-500">{t('courseMap.noEngramsFound')}</li>
            )}
          </ul>
        </div>
      )}

      {/* Bottom Legend Overlay */}
      <div className="pointer-events-none absolute bottom-4 left-4 right-4 flex flex-wrap items-end justify-between gap-3">
        <div className="pointer-events-auto flex flex-wrap items-center gap-3 rounded-2xl border border-white/10 bg-slate-900/80 px-3.5 py-2 text-[11px] font-medium text-slate-300 backdrop-blur-md shadow-lg">
          <span className="flex items-center gap-1.5" title="Bigger + brighter yellow = a section you've mastered">
            <span className="h-3 w-3 rounded-full bg-amber-400 shadow-sm shadow-amber-400/50" />
            <span>{t('courseMap.masteredEngram')} ({stats.masteredCount})</span>
          </span>
          <span className="flex items-center gap-1.5" title="Smaller, dim yellow = a section you haven't passed yet">
            <span className="h-2.5 w-2.5 rounded-full bg-amber-700 opacity-60" />
            <span>{t('courseMap.pendingEngram')} ({stats.engramsCount - stats.masteredCount})</span>
          </span>
          <span className="flex items-center gap-1.5" title="A concept/term that appears in 2+ sections, connecting them">
            <span className="h-2.5 w-2.5 rounded-full bg-sky-400" />
            <span>{t('courseMap.bridgeConcept')} ({stats.bridgesCount})</span>
          </span>
          <span className="flex items-center gap-1.5" title="A concept that dominates the whole course (shows up in many sections)">
            <span className="h-3.5 w-3.5 rounded-full bg-sky-400 shadow-sm shadow-sky-400/50" />
            <span>{t('courseMap.centralSun')} ({stats.sunsCount})</span>
          </span>
          <span className="flex items-center gap-1.5" title="Faint indigo line = the section-to-section course order, always drawn even without shared concepts">
            <span className="h-0.5 w-4 rounded-full bg-indigo-400/70" />
            <span>{t('courseMap.coursePath')}</span>
          </span>
        </div>

        <div className="pointer-events-auto hidden md:block rounded-xl border border-white/10 bg-slate-900/70 px-3 py-1.5 text-[10px] text-slate-400 backdrop-blur-md">
          {t('courseMap.rotateHint')}
        </div>
      </div>

      {/* Concept detail modal — click a cyan/amber node to see which
          engrams reference it, and jump straight to one. */}
      {selectedEntity && (
        <div
          className="absolute inset-0 z-20 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          onClick={() => setSelectedEntity(null)}
        >
          <div
            className="w-full max-w-md rounded-2xl border border-white/10 bg-slate-900 p-5 shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <p className="text-[10px] font-semibold uppercase tracking-wide text-sky-400">
                  {selectedEntity.EntityType || t('courseMap.conceptTitle')}
                </p>
                <h3 className="mt-0.5 text-base font-bold text-white">{selectedEntity.CanonicalName}</h3>
              </div>
              <button
                type="button"
                onClick={() => setSelectedEntity(null)}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-white/10 hover:text-white"
                aria-label={t('knowledgeGraph.close')}
              >
                <X className="h-4 w-4" />
              </button>
            </div>

            <p className="mt-3 text-xs text-slate-400">
              {t('courseMap.foundInEngrams', { count: selectedEntitySections.length })}
            </p>

            <ul className="mt-2 max-h-72 space-y-1.5 overflow-y-auto pr-1">
              {selectedEntitySections.map((sec) => {
                const round = sectionRounds[sec.StructuredLearningDocumentSectionId];
                const isMastered = round?.status === 'Passed';
                const isAvailable = isMastered || isSectionAvailable(sec);
                const label = (
                  <>
                    <span className={`h-2 w-2 shrink-0 rounded-full ${isMastered ? 'bg-emerald-400' : isAvailable ? 'bg-slate-400' : 'bg-slate-600'}`} />
                    <span className="min-w-0 flex-1 truncate">
                      {sec.SortOrder + 1}. {sec.SubchapterTitle || sec.ChapterTitle}
                    </span>
                  </>
                );
                return (
                  <li key={sec.StructuredLearningDocumentSectionId}>
                    {isAvailable ? (
                      <Link
                        to={`/structured-learning/${document.StructuredLearningDocumentId}/sections/${sec.StructuredLearningDocumentSectionId}`}
                        onClick={() => {
                          setSelectedEntity(null);
                          onSelectSection?.(sec);
                        }}
                        className="flex w-full items-center gap-2 rounded-xl border border-white/10 bg-white/[0.03] px-3 py-2 text-left text-sm text-slate-200 transition hover:border-sky-400/50 hover:bg-white/[0.06]"
                      >
                        {label}
                      </Link>
                    ) : (
                      <div
                        className="flex w-full cursor-not-allowed items-center gap-2 rounded-xl border border-white/5 bg-white/[0.01] px-3 py-2 text-sm text-slate-500"
                        title={t('courseMap.engramLockedHint')}
                      >
                        {label}
                        <Lock className="h-3.5 w-3.5 shrink-0 text-slate-600" />
                      </div>
                    )}
                  </li>
                );
              })}
              {selectedEntitySections.length === 0 && (
                <li className="text-xs text-slate-500">{t('courseMap.noEngramsFound')}</li>
              )}
            </ul>
          </div>
        </div>
      )}
    </div>
  );
};
