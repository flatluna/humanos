import { useState } from 'react';
import { X, Send, Sparkles, Bot, User } from 'lucide-react';
import { useLanguage } from '../../context/LanguageContext';
import { useFamily } from '../../context/FamilyContext';

export function FamilyCoachModal() {
  const { lang } = useLanguage();
  const { isAiCoachOpen, setIsAiCoachOpen } = useFamily();
  const [messages, setMessages] = useState([
    {
      sender: 'coach',
      text:
        lang === 'es'
          ? '¡Hola Carlos! Soy tu Family Coach AI de EngramsIQ. Estoy analizando las metas de la Familia Mendoza-Ríos para esta semana en Austin y CDMX. ¿Deseas revisar el cronograma del examen SAT de Sofía, la preparación de Mateo para robótica, o el balance de sueño y pantallas de la familia?'
          : "Hello Carlos! I'm your EngramsIQ Family Coach AI. I'm actively reviewing the Mendoza-Ríos household priorities this week across Austin and Mexico City. Would you like to review Sofía's SAT prep cadence, Mateo's robotics schedule, or the family sleep and digital sunset balance?",
      time: '12:30 PM',
    },
  ]);
  const [inputVal, setInputVal] = useState('');

  if (!isAiCoachOpen) return null;

  const handleSend = () => {
    if (!inputVal.trim()) return;
    const userText = inputVal.trim();
    setInputVal('');

    const newMsgs = [
      ...messages,
      { sender: 'user', text: userText, time: 'Ahora' },
    ];
    setMessages(newMsgs);

    setTimeout(() => {
      let reply = '';
      if (lang === 'es') {
        reply = `Entendido. He analizado el plan para "${userText}". Recomendación: Aseguremos un bloque de concentración bilingüe de 45 minutos antes de la cena, sincronizado con el calendario familiar de Google y el plan 529 de inversiones. ¿Deseas que active un recordatorio suave para los integrantes?`;
      } else {
        reply = `Understood. Analyzing "${userText}" across the family roadmap. Recommendation: Let's schedule a dedicated 45-minute focused study block before dinner, synced with the family Google calendar and college savings goals. Would you like me to push a gentle reminder to the household?`;
      }
      setMessages((prev) => [...prev, { sender: 'coach', text: reply, time: 'Ahora' }]);
    }, 600);
  };

  const suggestions =
    lang === 'es'
      ? [
          '¿Cómo optimizar el horario de estudio de Sofía para el SAT?',
          'Estrategia de inversión 529 vs Fideicomiso en México',
          'Ideas para la noche familiar sin pantallas',
          'Ruta de robótica y matemáticas para Mateo',
        ]
      : [
          'How to optimize Sofías SAT study routine?',
          '529 College Plan vs LATAM Educational Trust',
          'Ideas for our weekly screen-free family evening',
          'Robotics and Math competition roadmap for Mateo',
        ];

  return (
    <div className="fixed inset-0 z-50 flex justify-end bg-black/40 backdrop-blur-xs transition-opacity animate-fade-in">
      <div className="flex h-full w-full max-w-lg flex-col bg-white shadow-2xl dark:bg-[#0f172a] border-l border-warm-200 dark:border-white/10">
        {/* Drawer Header */}
        <div className="flex items-center justify-between border-b border-warm-200 px-6 py-4 dark:border-white/10">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-gradient-to-br from-emerald-600 to-teal-700 text-white shadow-md shadow-emerald-600/30">
              <Sparkles className="h-5 w-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-warm-900 dark:text-white flex items-center gap-2">
                Family Coach AI
                <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-[10px] font-bold text-emerald-800 dark:bg-emerald-500/20 dark:text-emerald-300">
                  Online
                </span>
              </h3>
              <p className="text-xs text-warm-500 dark:text-warm-400">
                {lang === 'es' ? 'Mentoría Bilingüe y Bienestar Familiar' : 'Bilingual Family Mentorship & Wellness'}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => setIsAiCoachOpen(false)}
            className="flex h-8 w-8 items-center justify-center rounded-full text-warm-500 hover:bg-warm-100 dark:text-warm-400 dark:hover:bg-white/10"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Messages list */}
        <div className="flex-1 overflow-y-auto p-6 space-y-4">
          {messages.map((msg, idx) => (
            <div
              key={idx}
              className={`flex items-start gap-3 ${msg.sender === 'user' ? 'flex-row-reverse' : ''}`}
            >
              <div
                className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-xs font-bold text-white shadow-sm ${
                  msg.sender === 'coach'
                    ? 'bg-gradient-to-br from-emerald-600 to-teal-700'
                    : 'bg-warm-700 dark:bg-warm-600'
                }`}
              >
                {msg.sender === 'coach' ? <Bot className="h-4 w-4" /> : <User className="h-4 w-4" />}
              </div>
              <div
                className={`max-w-[85%] rounded-2xl px-4 py-3 text-sm leading-relaxed ${
                  msg.sender === 'user'
                    ? 'bg-emerald-600 text-white shadow-sm'
                    : 'bg-warm-100 text-warm-900 dark:bg-warm-800/80 dark:text-warm-100'
                }`}
              >
                <p>{msg.text}</p>
                <span className="mt-1 block text-[10px] opacity-70 text-right">{msg.time}</span>
              </div>
            </div>
          ))}

          {/* Quick Prompts */}
          <div className="pt-2">
            <p className="text-[11px] font-bold uppercase tracking-wider text-warm-400 dark:text-warm-500 mb-2">
              {lang === 'es' ? 'Preguntas sugeridas' : 'Suggested questions'}
            </p>
            <div className="flex flex-col gap-2">
              {suggestions.map((s, i) => (
                <button
                  key={i}
                  type="button"
                  onClick={() => {
                    setInputVal(s);
                  }}
                  className="rounded-xl border border-warm-200 bg-warm-50/70 px-3 py-2 text-left text-xs text-warm-700 transition hover:border-emerald-400 hover:bg-emerald-50 dark:border-white/10 dark:bg-white/5 dark:text-warm-200 dark:hover:bg-emerald-950/30"
                >
                  💡 {s}
                </button>
              ))}
            </div>
          </div>
        </div>

        {/* Input box */}
        <div className="border-t border-warm-200 p-4 dark:border-white/10">
          <form
            onSubmit={(e) => {
              e.preventDefault();
              handleSend();
            }}
            className="flex items-center gap-2"
          >
            <input
              type="text"
              value={inputVal}
              onChange={(e) => setInputVal(e.target.value)}
              placeholder={
                lang === 'es'
                  ? 'Pregunta al Family Coach sobre metas, estudios o bienestar...'
                  : 'Ask Family Coach about goals, academics, or wellness...'
              }
              className="flex-1 rounded-xl border border-warm-300 bg-white px-4 py-2.5 text-xs text-warm-900 focus:border-emerald-500 focus:outline-none focus:ring-1 focus:ring-emerald-500 dark:border-white/10 dark:bg-warm-900 dark:text-white"
            />
            <button
              type="submit"
              className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-emerald-600 text-white shadow-sm transition hover:bg-emerald-700"
            >
              <Send className="h-4 w-4" />
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
