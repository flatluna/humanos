import React, { useMemo, useRef, useEffect, useState, useCallback } from 'react';
import ForceGraph3D, { type ForceGraphMethods } from 'react-force-graph-3d';
import * as THREE from 'three';
import { useTranslation } from 'react-i18next';
import { Sparkles, Maximize2, Minimize2, RotateCcw } from 'lucide-react';
import { getMemoryColor, getRetrievability } from '../../brain/memoryScience';
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
  linkType: 'connection' | 'membership';
}

export const CourseGalaxy3DGraph: React.FC<CourseGalaxy3DGraphProps> = ({
  document,
  entities,
  connections,
  sectionRounds,
  onSelectSection,
  height = 540,
}) => {
  const { t } = useTranslation();
  const containerRef = useRef<HTMLDivElement>(null);
  const fgRef = useRef<ForceGraphMethods | undefined>(undefined);
  const [dimensions, setDimensions] = useState({ width: 800, height });
  const [isFullscreen, setIsFullscreen] = useState(false);

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
  const { graphData, stats } = useMemo(() => {
    const nodes: GalaxyNode[] = [];
    const links: GalaxyLink[] = [];
    const nodeIds = new Set<string>();

    const excludeEntityNames = new Set([
      'developer',
      'azure apis + microservices guide',
      'organization',
    ]);

    const sunEntityNames = new Set(['api', 'microservice']);

    // 1. Identify Suns (Hyper-Hubs) and Bridges (2-5 occurrences)
    const sunEntities: StructuredLearningDocumentEntity[] = [];
    const bridgeEntities: StructuredLearningDocumentEntity[] = [];

    for (const ent of entities) {
      const lowerName = ent.CanonicalName.trim().toLowerCase();
      if (excludeEntityNames.has(lowerName)) continue;

      if (sunEntityNames.has(lowerName)) {
        // Keep unique suns
        if (!sunEntities.some((s) => s.CanonicalName.toLowerCase() === lowerName)) {
          sunEntities.push(ent);
        }
      } else if (ent.Occurrences.length >= 2 && ent.Occurrences.length <= 5) {
        bridgeEntities.push(ent);
      }
    }

    // 2. Add Hyper-Hub Suns (Golden central cores)
    for (const sun of sunEntities) {
      nodes.push({
        id: sun.EntityId,
        name: sun.CanonicalName,
        rawTitle: sun.CanonicalName,
        nodeType: 'sun',
        color: '#fbbf24', // Amber/Gold glowing
        size: 14,
        occurrencesCount: sun.Occurrences.length,
        entityType: sun.EntityType,
        entity: sun,
      });
      nodeIds.add(sun.EntityId);
    }

    // 3. Add Bridge Entities (Cyan/Sky nodes)
    for (const bridge of bridgeEntities) {
      nodes.push({
        id: bridge.EntityId,
        name: bridge.CanonicalName,
        rawTitle: bridge.CanonicalName,
        nodeType: 'bridge',
        color: '#38bdf8', // Cyan / Sky blue
        size: 5,
        occurrencesCount: bridge.Occurrences.length,
        entityType: bridge.EntityType,
        entity: bridge,
      });
      nodeIds.add(bridge.EntityId);
    }

    // 4. Add all 41 Engrams (Course Sections)
    const masteredEngramIds = new Set<string>();
    let masteredCount = 0;

    for (const sec of document.Sections) {
      const round = sectionRounds[sec.StructuredLearningDocumentSectionId];
      const isMastered = round?.status === 'Passed';
      const reviewCount = round?.roundNumber ?? 1;
      const retrievability = isMastered ? getRetrievability(round.completedDate, reviewCount) : null;
      const recallPercent = retrievability !== null ? Math.round(retrievability * 100) : null;

      if (isMastered) {
        masteredEngramIds.add(sec.StructuredLearningDocumentSectionId);
        masteredCount++;
      }

      const engramColor = isMastered
        ? getMemoryColor(retrievability) // Vibrant emerald green / amber / coral based on FSRS
        : '#475569'; // Dim slate gray for pending

      nodes.push({
        id: sec.StructuredLearningDocumentSectionId,
        name: `${sec.SortOrder + 1}. ${sec.SubchapterTitle || sec.ChapterTitle}`,
        rawTitle: sec.SubchapterTitle || sec.ChapterTitle,
        nodeType: 'engram',
        isMastered,
        recallPercent,
        color: engramColor,
        size: isMastered ? 11 : 7.5,
        section: sec,
      });
      nodeIds.add(sec.StructuredLearningDocumentSectionId);
    }

    // 5. Connect Engrams to their Bridge / Sun Entities
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

    // 6. Connect high-confidence entity-to-entity relations (Confidence >= 0.8)
    for (const conn of connections) {
      const conf = conn.Confidence ?? 0.85;
      if (conf >= 0.8 && nodeIds.has(conn.SourceEntityId) && nodeIds.has(conn.TargetEntityId)) {
        const key = `${conn.SourceEntityId}->${conn.TargetEntityId}`;
        if (!existingLinkKeys.has(key)) {
          existingLinkKeys.add(key);
          const isMasteredPath =
            conn.SourceSectionId !== null && masteredEngramIds.has(conn.SourceSectionId);
          links.push({
            source: conn.SourceEntityId,
            target: conn.TargetEntityId,
            label: conn.RelationshipLabel,
            confidence: conf,
            isMasteredPath,
            linkType: 'connection',
          });
        }
      }
    }

    return {
      graphData: { nodes, links },
      stats: {
        totalNodes: nodes.length,
        engramsCount: document.Sections.length,
        masteredCount,
        bridgesCount: bridgeEntities.length,
        sunsCount: sunEntities.length,
        totalLinks: links.length,
      },
    };
  }, [document, entities, connections, sectionRounds]);

  // Adjust camera & physics on load
  useEffect(() => {
    if (fgRef.current) {
      fgRef.current.d3Force('charge')?.strength(-140);
      fgRef.current.d3Force('link')?.distance(45);
    }
  }, [graphData]);

  const handleResetCamera = useCallback(() => {
    if (fgRef.current) {
      fgRef.current.cameraPosition({ x: 0, y: 0, z: 280 }, { x: 0, y: 0, z: 0 }, 1000);
    }
  }, []);

  const handleNodeClick = useCallback(
    (node: GalaxyNode) => {
      if (node.nodeType === 'engram' && node.section && onSelectSection) {
        onSelectSection(node.section);
      }
    },
    [onSelectSection]
  );

  // Custom Three.js rendering for glowing galaxy spheres and suns
  const nodeThreeObject = useCallback((node: GalaxyNode) => {
    const group = new THREE.Group();

    const isSun = node.nodeType === 'sun';
    const isMastered = node.nodeType === 'engram' && node.isMastered;
    const isPendingEngram = node.nodeType === 'engram' && !node.isMastered;
    const isBridge = node.nodeType === 'bridge';

    // Core sphere
    const geometry = new THREE.SphereGeometry(node.size, isSun ? 24 : 16, isSun ? 24 : 16);
    const material = new THREE.MeshStandardMaterial({
      color: node.color,
      emissive: node.color,
      emissiveIntensity: isSun ? 0.9 : isMastered ? 0.75 : isBridge ? 0.35 : 0.08,
      roughness: isSun ? 0.1 : 0.4,
      metalness: isSun ? 0.1 : 0.3,
      transparent: isPendingEngram,
      opacity: isPendingEngram ? 0.45 : 0.95,
    });
    const mesh = new THREE.Mesh(geometry, material);
    group.add(mesh);

    // Glowing halo aura for Suns and Mastered Engrams
    if (isSun || isMastered) {
      const haloGeo = new THREE.SphereGeometry(node.size * 1.35, 16, 16);
      const haloMat = new THREE.MeshBasicMaterial({
        color: node.color,
        transparent: true,
        opacity: isSun ? 0.28 : 0.22,
        side: THREE.BackSide,
      });
      group.add(new THREE.Mesh(haloGeo, haloMat));
    }

    return group;
  }, []);

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
        graphData={graphData}
        backgroundColor="#020617"
        showNavInfo={false}
        nodeThreeObject={nodeThreeObject as any}
        nodeLabel={(node: any) => {
          const n = node as GalaxyNode;
          if (n.nodeType === 'engram') {
            return `
              <div style="background: rgba(15, 23, 42, 0.92); border: 1px solid ${n.color}; border-radius: 10px; padding: 8px 12px; font-family: sans-serif; font-size: 12px; color: #fff; box-shadow: 0 4px 20px rgba(0,0,0,0.6); backdrop-filter: blur(8px);">
                <div style="font-weight: 700; color: ${n.color}; margin-bottom: 2px;">⚡ ${n.name}</div>
                <div style="font-size: 11px; color: #94a3b8;">${n.isMastered ? `✓ ${t('courseMap.masteredEngram')} (${n.recallPercent}%)` : `○ ${t('courseMap.pendingEngram')}`}</div>
                <div style="margin-top: 4px; font-size: 10px; color: #38bdf8; font-style: italic;">${t('courseMap.clickToInspect')}</div>
              </div>
            `;
          }
          if (n.nodeType === 'sun') {
            return `
              <div style="background: rgba(15, 23, 42, 0.92); border: 1px solid #fbbf24; border-radius: 10px; padding: 8px 12px; font-family: sans-serif; font-size: 12px; color: #fff; box-shadow: 0 4px 20px rgba(251, 191, 36, 0.3);">
                <div style="font-weight: 800; color: #fbbf24; font-size: 13px;">☀️ ${n.name}</div>
                <div style="font-size: 10px; color: #e2e8f0; margin-top: 2px;">${t('courseMap.centralSun')} · ${n.occurrencesCount} ${t('courseMap.occurrences', { count: n.occurrencesCount })}</div>
              </div>
            `;
          }
          return `
            <div style="background: rgba(15, 23, 42, 0.92); border: 1px solid #38bdf8; border-radius: 8px; padding: 6px 10px; font-family: sans-serif; font-size: 11px; color: #fff;">
              <div style="font-weight: 600; color: #38bdf8;">🔗 ${n.name}</div>
              <div style="font-size: 10px; color: #94a3b8;">${n.entityType || 'Concept'} · ${n.occurrencesCount} ${t('courseMap.occurrences', { count: n.occurrencesCount })}</div>
            </div>
          `;
        }}
        linkColor={(link: any) =>
          link.isMasteredPath ? 'rgba(16, 185, 129, 0.75)' : 'rgba(148, 163, 184, 0.22)'
        }
        linkWidth={(link: any) => (link.isMasteredPath ? 2.2 : 0.8)}
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

      {/* Bottom Legend Overlay */}
      <div className="pointer-events-none absolute bottom-4 left-4 right-4 flex flex-wrap items-end justify-between gap-3">
        <div className="pointer-events-auto flex flex-wrap items-center gap-3 rounded-2xl border border-white/10 bg-slate-900/80 px-3.5 py-2 text-[11px] font-medium text-slate-300 backdrop-blur-md shadow-lg">
          <span className="flex items-center gap-1.5">
            <span className="h-3 w-3 rounded-full bg-emerald-500 shadow-sm shadow-emerald-500/50" />
            <span>{t('courseMap.masteredEngram')} ({stats.masteredCount})</span>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full bg-slate-600 opacity-60" />
            <span>{t('courseMap.pendingEngram')} ({stats.engramsCount - stats.masteredCount})</span>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="h-2.5 w-2.5 rounded-full bg-sky-400" />
            <span>{t('courseMap.bridgeConcept')} ({stats.bridgesCount})</span>
          </span>
          <span className="flex items-center gap-1.5">
            <span className="h-3.5 w-3.5 rounded-full bg-amber-400 shadow-sm shadow-amber-400/50" />
            <span>{t('courseMap.centralSun')} ({stats.sunsCount})</span>
          </span>
        </div>

        <div className="pointer-events-auto hidden md:block rounded-xl border border-white/10 bg-slate-900/70 px-3 py-1.5 text-[10px] text-slate-400 backdrop-blur-md">
          {t('courseMap.rotateHint')}
        </div>
      </div>
    </div>
  );
};
