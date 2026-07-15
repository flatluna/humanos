import { CheckCircle2 } from 'lucide-react';

export default function SuccessIndicator() {
  return (
    <div className="mb-6">
      <CheckCircle2 className="w-16 h-16 text-green-600 mx-auto mb-4" />
      <h2 className="text-2xl font-bold text-gray-900">Tu capability está lista</h2>
    </div>
  );
}
