/**
 * Рендеринг KPI метрик
 */

import { DomManager } from '../services/dom.service.js';

export class KpiRenderer {
    constructor(domManager) {
        this.dom = domManager;
    }

    /**
     * Рендеринг KPI для одиночного зображення
     */
    renderSingleKpi(json, containerId = "singleKpi") {
        const k = this.dom.$(containerId);
        if (!k) return;
        k.innerHTML = "";
        if (!json) return;

        if (json.engine) k.appendChild(this.dom.createPill("Двигун: " + json.engine));
        if (typeof json.elapsedMs === "number") {
            k.appendChild(this.dom.createPill("Detect: " + json.elapsedMs.toFixed(1) + " мс"));
        }
        if (Array.isArray(json.faces)) {
            k.appendChild(this.dom.createPill("Облич: " + json.faces.length));
        }

        if (json.timings) {
            const t = json.timings;
            if (typeof t.totalMs === "number") {
                k.appendChild(this.dom.createPill("Загалом: " + t.totalMs.toFixed(1) + " мс"));
            }
            if (typeof t.detectMs === "number") {
                k.appendChild(this.dom.createPill("Detect: " + t.detectMs.toFixed(1) + " мс"));
            }
            if (t.antiSpoofMs > 0) {
                k.appendChild(this.dom.createPill("AntiSpoof: " + t.antiSpoofMs.toFixed(1) + " мс"));
            }
            if (typeof t.anonymizeMs === "number") {
                k.appendChild(this.dom.createPill("Anon: " + t.anonymizeMs.toFixed(1) + " мс"));
            }
            if (typeof t.encodeMs === "number") {
                k.appendChild(this.dom.createPill("Encode: " + t.encodeMs.toFixed(1) + " мс"));
            }
            if (t.evaluationMs > 0) {
                k.appendChild(this.dom.createPill("Eval: " + t.evaluationMs.toFixed(1) + " мс"));
            }
        }

        if (json.metrics) {
            const m = json.metrics;
            if (typeof m.meanIoU === "number") {
                k.appendChild(this.dom.createPill("IoU: " + m.meanIoU.toFixed(3)));
            }
            if (typeof m.precision === "number") {
                k.appendChild(this.dom.createPill("Precision: " + m.precision.toFixed(3)));
            }
            if (typeof m.recall === "number") {
                k.appendChild(this.dom.createPill("Recall: " + m.recall.toFixed(3)));
            }
            if (typeof m.f1 === "number") {
                k.appendChild(this.dom.createPill("F1: " + m.f1.toFixed(3)));
            }
            if (typeof m.ssim === "number") {
                k.appendChild(this.dom.createPill("SSIM: " + m.ssim.toFixed(4)));
            }
            if (typeof m.psnrDb === "number") {
                k.appendChild(this.dom.createPill("PSNR: " + m.psnrDb.toFixed(1) + " dB"));
            }
        }
    }

    /**
     * Рендеринг KPI для пакетної обробки
     */
    renderBatchKpi(json, containerId = "batchKpi") {
        const k = this.dom.$(containerId);
        if (!k) return;
        k.innerHTML = "";
        if (!json) return;

        k.appendChild(this.dom.createPill("RunId: " + json.runId));
        k.appendChild(this.dom.createPill("Усього: " + json.totalFiles));
        k.appendChild(this.dom.createPill("Успішно: " + json.processedOk));
        k.appendChild(this.dom.createPill("Помилок: " + json.failed));
        
        if (typeof json.elapsedMs === "number") {
            k.appendChild(this.dom.createPill("Час: " + json.elapsedMs.toFixed(1) + " мс"));
        }
    }
}
