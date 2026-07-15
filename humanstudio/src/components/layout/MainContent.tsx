import React from 'react';

interface MainContentProps {
  children: React.ReactNode;
}

const MainContent: React.FC<MainContentProps> = ({ children }) => {
  return (
    <main className="flex-1 overflow-auto pt-16">
      <div className="p-8 max-w-7xl mx-auto">
        {children}
      </div>
    </main>
  );
};

export default MainContent;
