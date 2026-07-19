import { Route, Routes } from 'react-router-dom';
import AppShell from '../components/layout/AppShell';
import HomePage from '../pages/HomePage';
import SubjectCapabilitiesPage from '../pages/SubjectCapabilitiesPage';
import CapabilityGraphMapPage from '../pages/CapabilityGraphMapPage';
import NodeWorkflowPage from '../pages/NodeWorkflowPage';

/**
 * Final navigation (decided — see
 * /memories/repo/student-graph-ui-redesign-final-design.md, Option A):
 * Home → Subjects → Capabilities → Capability Graph Map → Node.
 * All routes render inside AppShell (top bar with user/email/language).
 */
export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/subjects/:subjectCode" element={<SubjectCapabilitiesPage />} />
        <Route path="/capabilities/:capabilityId" element={<CapabilityGraphMapPage />} />
        <Route path="/capabilities/:capabilityId/nodes/:nodeId" element={<NodeWorkflowPage />} />
      </Route>
    </Routes>
  );
}
