/**
 * Головний модуль додатку
 */

import { config } from './config.js';
import { ApiClient } from './services/api.service.js';
import { DomManager } from './services/dom.service.js';
import { ThemeManager } from './utils/theme.js';
import { ImageModal } from './utils/modal.js';
import { SingleImagePage } from './pages/single.page.js';
import { BatchPage } from './pages/batch.page.js';

class FaceAnonymizerApp {
    constructor() {
        this.dom = new DomManager();
        this.api = new ApiClient();
        this.themeManager = null;
        this.imageModal = null;
        this.singlePage = null;
        this.batchPage = null;
    }

    async init() {
        // Ініціалізація утиліт
        this.themeManager = new ThemeManager();
        this.imageModal = new ImageModal();
        window.imageModal = this.imageModal; // Глобальний доступ для зворотної сумісності

        // Ініціалізація навігації
        this.setupTabs();
        this.setupSliders();
        this.dom.preventDefaultDragBehavior();

        // Ініціалізація сторінок
        this.singlePage = new SingleImagePage(this.api, this.dom);
        this.batchPage = new BatchPage(this.api, this.dom);

        this.singlePage.init();
        this.batchPage.init();

        // Завантаження capabilities
        await this.loadCapabilities();
    }

    setupTabs() {
        document.querySelectorAll(".tab").forEach(btn => {
            btn.addEventListener("click", () => {
                this.dom.switchTab(btn.dataset.tab);
            });
        });
    }

    setupSliders() {
        const strengthSlider = this.dom.$("strength");
        const strengthLabel = this.dom.$("strengthLabel");
        
        if (strengthSlider && strengthLabel) {
            strengthSlider.addEventListener("input", () => {
                strengthLabel.textContent = strengthSlider.value;
            });
        }

        const batchStrengthSlider = this.dom.$("batchStrength");
        const batchStrengthLabel = this.dom.$("batchStrengthLabel");
        
        if (batchStrengthSlider && batchStrengthLabel) {
            batchStrengthSlider.addEventListener("input", () => {
                batchStrengthLabel.textContent = batchStrengthSlider.value;
            });
        }
    }

    async loadCapabilities() {
        try {
            const caps = await this.api.loadCapabilities();
            this.dom.setJson(caps);

            const engines = caps.engines || [config.defaults.engine];
            const methods = caps.methods || [config.defaults.method];

            // Single page selects
            this.dom.setSelectOptions(this.dom.$("engine"), engines);
            this.dom.setSelectOptions(this.dom.$("method"), methods);

            // Batch page selects
            this.dom.setSelectOptions(this.dom.$("batchEngine"), engines);
            this.dom.setSelectOptions(this.dom.$("batchMethod"), methods);

            // Встановлення дефолтних значень
            if (engines.includes(config.defaults.engine)) {
                if (this.dom.$("engine")) this.dom.$("engine").value = config.defaults.engine;
                if (this.dom.$("batchEngine")) this.dom.$("batchEngine").value = config.defaults.engine;
            }

            if (methods.includes(config.defaults.method)) {
                if (this.dom.$("method")) this.dom.$("method").value = config.defaults.method;
                if (this.dom.$("batchMethod")) this.dom.$("batchMethod").value = config.defaults.method;
            }
        } catch (e) {
            this.dom.setJson({ error: "Failed to load capabilities: " + e.message });
        }
    }
}

// Ініціалізація додатку
document.addEventListener("DOMContentLoaded", () => {
    const app = new FaceAnonymizerApp();
    app.init().catch(e => {
        console.error("Init failed:", e);
        const dom = new DomManager();
        dom.setJson({ error: "Init failed: " + e.message });
    });
});
