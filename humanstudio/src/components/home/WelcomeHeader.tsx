import React from 'react';

interface WelcomeHeaderProps {
  userName: string;
}

const WelcomeHeader: React.FC<WelcomeHeaderProps> = ({ userName }) => {
  return (
    <div className="mb-8">
      <h1 className="text-4xl font-bold text-gray-900 mb-2">
        Bienvenido de vuelta, {userName}
      </h1>
      <p className="text-lg text-gray-600">
        Continúa desarrollando tus capacidades.
      </p>
    </div>
  );
};

export default WelcomeHeader;
