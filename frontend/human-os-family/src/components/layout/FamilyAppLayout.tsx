import { Outlet } from 'react-router';
import { FamilyHeader } from './FamilyHeader';
import { FamilySidebar } from './FamilySidebar';
import { FamilyCoachModal } from '../agent/FamilyCoachModal';

export function FamilyAppLayout() {
  return (
    <div className="min-h-screen bg-warm-50/60 transition-colors duration-200 dark:bg-[#070a10]">
      <FamilyHeader />

      <div className="flex">
        <FamilySidebar />

        <main className="min-w-0 flex-1 px-4 py-8 sm:px-6 lg:px-10 max-w-7xl mx-auto w-full">
          <Outlet />
        </main>
      </div>

      <FamilyCoachModal />
    </div>
  );
}
