export type ContentLevel = 'Introductory' | 'Intermediate' | 'Advanced';

export class ProfessorContentRating {
  constructor(
    public readonly title: string,
    public readonly level: ContentLevel,
    public readonly audience: string,
    public readonly academicValue: string,
    public readonly modules: readonly string[],
    public readonly professorNote: string,
  ) {}
}

export const humanOsContentRating = new ProfessorContentRating(
  'Human OS',
  'Advanced',
  'University students, educators, and professionals preparing for complex real-world situations.',
  'Human OS is an advanced AI learning application built with the Human OS Agentic AI framework. It helps students learn through guided practice, reflection, and realistic experiences instead of relying only on lectures or written exercises.',
  [
    'Structured learning paths that turn a subject into clear lessons and practice activities.',
    'Studio Labs that create simulations for situations such as employer interviews, client conversations, and university thesis defenses.',
    'Video productions that turn a teaching objective into short, focused learning moments.',
    'Feedback and reflection activities that help students improve their decisions, communication, and confidence.',
  ],
  'From a professor’s perspective, the app is strongest as an experiential learning environment: students can rehearse difficult moments, receive guidance, and try again in a low-risk setting before applying the skill in real life.',
);