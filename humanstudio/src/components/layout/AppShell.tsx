import React from 'react';
import { Outlet } from 'react-router-dom';
import TopBar from './TopBar';
import Sidebar from './Sidebar';
import MainContent from './MainContent';
import type { User } from '../../types';

const AppShell: React.FC = () => {
  const [sidebarOpen, setSidebarOpen] = React.useState(true);

  // Mock user data - replace with real auth context later
  const mockUser: User = {
    name: 'Jorge Pérez',
    email: 'jorge.perez@humanstudio.dev',
    oid: '123e4567-e89b-12d3-a456-426614174000',
    avatarUrl: undefined,
  };

  const handleSignOut = () => {
    console.log('Sign out clicked');
    // TODO: Implement real sign out logic
  };

  const handleOpenSettings = () => {
    console.log('Settings clicked');
    // TODO: Navigate to settings page
  };

  const handleSearch = (query: string) => {
    console.log('Search:', query);
    // TODO: Implement search logic
  };

  return (
    <div className="flex h-screen bg-gray-50">
      {/* Sidebar */}
      <Sidebar isOpen={sidebarOpen} onToggle={() => setSidebarOpen(!sidebarOpen)} />

      {/* Main area */}
      <div className="flex flex-col flex-1">
        {/* Top Bar */}
        <TopBar
          user={mockUser}
          onSearch={handleSearch}
          onOpenSettings={handleOpenSettings}
          onSignOut={handleSignOut}
        />

        {/* Main Content */}
        <MainContent>
          <Outlet />
        </MainContent>
      </div>
    </div>
  );
};

export default AppShell;
