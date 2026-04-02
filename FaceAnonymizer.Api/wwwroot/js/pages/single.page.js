/**
 * Модуль для роботи з одиночними зображеннями
 */

import { config } from '../config.js';
import { ProgressManager } from '../utils/progress.js';
import { ImageModal } from '../utils/modal.js';
import { SingleFileDropZone } from '../components/dropzone.js';
import { KpiRenderer } from '../components/kpi.js';

export class SingleImagePage {
    constructor(apiClient, domManager) {
        this.api = apiClient;
        this.dom = domManager;
        this.progress = new ProgressManager('singleProgress', 'singleProgressBar', 'singleProgressText');
        this.kpiRenderer = new KpiRenderer(domManager);
    }

    init() {
        this.setupDropZone();
        this.setupActions();
        this.setupColorPicker();
        this.enableImagePreview();
    }

    setupDropZone() {
        new SingleFileDropZone('singleDropZone', 'image', 'singlePickedInfo', () => {
            const file = this.dom.$('image')?.files?.[0];
            const info = this.dom.$('singlePickedInfo');
            if (info) info.textContent = file ? `Обрано: ${file.name}` : "";
        });
    }

    setupActions() {
        // Detect button
        this.dom.$("btnDetect")?.addEventListener("click", async () => {
            await this.handleDetect();
        });

        // Anonymize button
        this.dom.$("btnAnon")?.addEventListener("click", async () => {
            await this.handleAnonymize();
        });
    }

    async handleDetect() {
        try {
            const file = this.dom.$("image")?.files?.[0];
            const engine = this.dom.$("engine")?.value || config.defaults.engine;
            
            this.progress.show("Виявлення облич…");
            const progressConfig = config.progress.single.detect;
            this.progress.animate(
                "Виявлення облич",
                progressConfig.softCeiling,
                progressConfig.intervalMs,
                progressConfig.speed
            );

            const url = `${config.api.endpoints.detect}?engine=${encodeURIComponent(engine)}&draw=true`;
            const json = await this.api.postMultipart(url, file);
            
            await this.progress.finish();
            this.dom.setJson(json);
            this.kpiRenderer.renderSingleKpi(json);
            this.setImageUrl(json.imageUrl);
        } catch (e) {
            this.progress.hide();
            this.dom.setJson({ error: e.message });
            this.kpiRenderer.renderSingleKpi(null);
            this.setImageUrl(null);
        }
    }

    async handleAnonymize() {
        try {
            const file = this.dom.$("image")?.files?.[0];
            const engine = this.dom.$("engine")?.value || config.defaults.engine;
            const method = this.dom.$("method")?.value || config.defaults.method;
            const strength = this.dom.$("strength")?.value || config.defaults.strength;
            const evaluate = !!this.dom.$("evaluate")?.checked;
            const color = method === "SolidColor" ? (this.dom.$("colorPicker")?.value || config.defaults.color) : undefined;

            let url = `${config.api.endpoints.anonymize}?engine=${encodeURIComponent(engine)}&method=${encodeURIComponent(method)}&strength=${encodeURIComponent(strength)}&evaluate=${evaluate}`;
            if (color) url += `&color=${encodeURIComponent(color)}`;

            this.progress.show("Анонімізація облич…");
            const progressConfig = config.progress.single.anonymize;
            this.progress.animate(
                "Анонімізація облич",
                progressConfig.softCeiling,
                progressConfig.intervalMs,
                progressConfig.speed
            );

            const json = await this.api.postMultipart(url, file);
            
            await this.progress.finish();
            this.dom.setJson(json);
            this.kpiRenderer.renderSingleKpi(json);
            this.setImageUrl(json.imageUrl);
        } catch (e) {
            this.progress.hide();
            this.dom.setJson({ error: e.message });
            this.kpiRenderer.renderSingleKpi(null);
            this.setImageUrl(null);
        }
    }

    setImageUrl(url) {
        const img = this.dom.$("imgOut");
        const wrap = this.dom.$("imgWrap");
        if (!img || !wrap) return;

        if (!url) {
            img.classList.add("is-hidden");
            img.classList.remove("clickable");
            wrap.classList.remove("is-hidden");
            wrap.textContent = "Немає зображення у відповіді.";
            return;
        }

        img.src = url;
        img.classList.remove("is-hidden");
        img.classList.add("clickable");
        wrap.classList.add("is-hidden");
    }

    setupColorPicker() {
        const methodSelect = this.dom.$("method");
        const colorField = this.dom.$("colorField");

        if (!methodSelect || !colorField) return;

        const toggleColorField = () => {
            const isSolidColor = methodSelect.value === "SolidColor";
            this.dom.toggleElement(colorField, isSolidColor);
        };

        methodSelect.addEventListener("change", toggleColorField);
        toggleColorField();
    }

    enableImagePreview() {
        const img = this.dom.$("imgOut");
        if (img) {
            ImageModal.makeClickable(img, () => img.src);
        }
    }
}
