/**
 * Управління темами
 */

export class ThemeManager {
    constructor() {
        this.themeBtn = document.getElementById("themeToggle");
        this.init();
    }

    init() {
        const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
        const savedTheme = localStorage.getItem("theme");
        const isDark = savedTheme ? savedTheme === "dark" : prefersDark;

        this.setTheme(isDark);

        if (this.themeBtn) {
            this.themeBtn.addEventListener("click", (e) => {
                e.preventDefault();
                e.stopPropagation();
                const newIsDark = document.documentElement.dataset.theme !== "dark";
                this.setTheme(newIsDark);
                localStorage.setItem("theme", newIsDark ? "dark" : "light");
            });
        }
    }

    setTheme(isDark) {
        const html = document.documentElement;
        html.dataset.theme = isDark ? "dark" : "light";
        if (this.themeBtn) {
            this.themeBtn.textContent = isDark ? "☀️" : "🌙";
        }
    }
}

// Швидка ініціалізація теми (блокує FOUC)
(function() {
    const saved = localStorage.getItem("theme");
    const prefersDark = window.matchMedia("(prefers-color-scheme: dark)").matches;
    const theme = saved || (prefersDark ? "dark" : "light");
    document.documentElement.dataset.theme = theme;
})();
