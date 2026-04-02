/**
 * Управління прогрес-баром
 */

export class ProgressManager {
    constructor(progressId, barId, textId) {
        this.progress = document.getElementById(progressId);
        this.bar = document.getElementById(barId);
        this.text = document.getElementById(textId);
        this.intervalId = null;
    }

    show(text = "Обробка…") {
        if (!this.progress) return;
        this.progress.classList.remove("is-hidden");
        if (this.bar) this.bar.style.width = "0%";
        if (this.text) this.text.textContent = text;
    }

    hide() {
        if (this.progress) {
            this.progress.classList.add("is-hidden");
        }
        if (this.bar) this.bar.style.width = "0%";
        this.stopAnimation();
    }

    setWidth(pct) {
        if (this.bar) {
            this.bar.style.width = Math.min(100, Math.max(0, pct)) + "%";
        }
    }

    updateText(text) {
        if (this.text) {
            this.text.textContent = text;
        }
    }

    /**
     * Нескінченна анімація прогресу
     * @param {string} label - текст для відображення
     * @param {number} softCeiling - поріг уповільнення (70-95)
     * @param {number} intervalMs - інтервал між тіками
     * @param {number} speed - множник швидкості фази 1
     */
    animate(label, softCeiling = 90, intervalMs = 100, speed = 0.08) {
        this.stopAnimation();
        
        let pct = 0;
        let ticks = 0;
        
        this.intervalId = setInterval(() => {
            ticks++;
            const seconds = (ticks * intervalMs) / 1000;

            if (pct < softCeiling) {
                // Фаза 1: швидке заповнення з уповільненням
                const remaining = softCeiling - pct;
                pct += Math.max(0.3, remaining * speed);
            } else {
                // Фаза 2: повільне повзання
                pct += 0.02;
            }

            if (pct > 99.5) pct = 99.5;
            this.setWidth(pct);

            // Оновлюємо текст з часом
            if (seconds < 2) {
                this.updateText(`${label}…`);
            } else {
                this.updateText(`${label}… ${seconds.toFixed(0)} с`);
            }
        }, intervalMs);
    }

    stopAnimation() {
        if (this.intervalId) {
            clearInterval(this.intervalId);
            this.intervalId = null;
        }
    }

    async finish() {
        this.stopAnimation();
        this.setWidth(100);
        this.updateText("Готово!");
        await new Promise(resolve => setTimeout(resolve, 700));
        this.hide();
    }
}
