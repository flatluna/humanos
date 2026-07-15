export interface FinalReviewHeaderProps {
  title: string;
  description: string;
}

export function FinalReviewHeader({ title, description }: FinalReviewHeaderProps) {
  return (
    <div className="mb-8">
      <h2 className="text-3xl font-bold text-gray-900 mb-2">{title}</h2>
      <p className="text-lg text-gray-600">{description}</p>
    </div>
  );
}
