import { Globe, Link2, AtSign } from "lucide-react";

const links = ["Vision", "Framework", "Capabilities", "Organizations", "Privacy", "Terms"];

export default function Footer() {
  return (
    <footer className="bg-white py-16 border-t border-slate-200">
      <div className="max-w-7xl mx-auto px-6">
        <div className="flex flex-col md:flex-row md:items-start md:justify-between gap-10">
          <div>
            <h3 className="text-lg font-semibold text-slate-900">
              Human Operative System
            </h3>
            <p className="mt-2 text-slate-500 max-w-xs">
              Strengthening Human Capability in the Age of AI
            </p>
          </div>

          <nav className="flex flex-wrap gap-x-8 gap-y-3">
            {links.map((link) => (
              <a
                key={link}
                href="#"
                className="text-sm font-medium text-slate-500 transition hover:text-slate-900"
              >
                {link}
              </a>
            ))}
          </nav>

          <div className="flex gap-4">
            {[Link2, Globe, AtSign].map((Icon, i) => (
              <a
                key={i}
                href="#"
                className="flex h-9 w-9 items-center justify-center rounded-full border border-slate-200 text-slate-500 transition hover:border-slate-300 hover:text-slate-900"
              >
                <Icon className="h-4 w-4" />
              </a>
            ))}
          </div>
        </div>

        <p className="mt-12 text-xs text-slate-400">
          © {new Date().getFullYear()} Human Operative System. All rights reserved.
        </p>
      </div>
    </footer>
  );
}
