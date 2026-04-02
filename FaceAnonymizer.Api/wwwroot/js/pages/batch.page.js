/**
 * Модуль для пакетної обробки
 */

import { config } from '../config.js';
import { ProgressManager } from '../utils/progress.js';
import { ImageModal } from '../utils/modal.js';
import { MultipleFilesDropZone } from '../components/dropzone.js';
import { KpiRenderer } from '../components/kpi.js';
import { utils } from '../utils/helpers.js';

export class BatchPage {
    constructor(apiClient, domManager) {
        this.api = apiClient;
        this.dom = domManager;
        this.progress = new ProgressManager('batchProgress', 'batchProgressBar', 'batchProgressText');
        this.kpiRenderer = new KpiRenderer(domManager);
    }

    init() {
        this.setupDropZone();
        this.setupActions();
        this.setupColorPicker();
        this.setupModeToggle();
        this.enableImagePreview();
        this.updatePickedInfo();
        this.setBatchMode("anon");
    }

    setupDropZone() {
        new MultipleFilesDropZone(
            'dropZone',
            'batchFiles',
            'pickedInfo',
            () => {
                this.dom.state.droppedFiles = [];
                this.updatePickedInfo();
            },
            this.dom.state.droppedFiles
        );
    }

    setupActions() {
        // Upload button
        this.dom.$("btnUpload")?.addEventListener("click", async () => {
            await this.handleUpload();
        });

        // Run batch button
        this.dom.$("btnRunBatch")?.addEventListener("click", async () => {
            await this.handleRunBatch();
        });

        // Navigation buttons
        this.dom.$("btnPrev")?.addEventListener("click", () => {
            this.showBatchImageByIndex(this.dom.state.batchIndex - 1);
        });

        this.dom.$("btnNext")?.addEventListener("click", () => {
            this.showBatchImageByIndex(this.dom.state.batchIndex + 1);
        });
    }

    setupModeToggle() {
        this.dom.$("btnModeAnon")?.addEventListener("click", () => {
            this.setBatchMode("anon");
        });

        this.dom.$("btnModeDetect")?.addEventListener("click", () => {
            this.setBatchMode("detect");
        });
    }

    setupColorPicker() {
        const methodSelect = this.dom.$("batchMethod");
        const colorField = this.dom.$("batchColorField");

        if (!methodSelect || !colorField) return;

        const toggleColorField = () => {
            const isSolidColor = methodSelect.value === "SolidColor";
            this.dom.toggleElement(colorField, isSolidColor);
        };

        methodSelect.addEventListener("change", toggleColorField);
        toggleColorField();
    }

    enableImagePreview() {
        const img = this.dom.$("batchImg");
        if (img) {
            ImageModal.makeClickable(img, () => img.src);
        }
    }

    getSelectedFiles() {
        const picked = Array.from(this.dom.$("batchFiles")?.files || []);
        const dropped = Array.from(this.dom.state.droppedFiles || []);
        return utils.uniqueFiles([...picked, ...dropped]);
    }

    updatePickedInfo() {
        const el = this.dom.$("pickedInfo");
        if (!el) return;
        const total = this.getSelectedFiles().length;
        el.textContent = total ? `Обрано файлів: ${total}` : "Файли не обрані.";
    }

    async handleUpload() {
        try {
            const files = this.getSelectedFiles();
            if (!files.length) throw new Error("Оберіть файли або перетягніть у зону.");
            
            const runId = (this.dom.$("runId")?.value || "").trim();

            this.progress.show(`Завантаження ${files.length} файлів…`);
            const progressConfig = config.progress.batch.upload;
            this.progress.animate(
                `Завантаження ${files.length} файлів`,
                progressConfig.softCeiling,
                progressConfig.intervalMs,
                progressConfig.speed
            );

            const json = await this.api.uploadBatch(runId, files);
            await this.progress.finish();

            this.dom.state.uploaded = json;
            this.dom.$("batchInfo").textContent = `Завантажено. runId=${json.runId}`;
            this.dom.$("btnRunBatch").disabled = false;
            this.dom.setJson(json);
        } catch (e) {
            this.progress.hide();
            this.dom.$("batchInfo").textContent = "Помилка: " + e.message;
            this.dom.$("btnRunBatch").disabled = true;
            this.dom.state.uploaded = null;
            this.dom.setJson({ error: e.message });
        }
    }

    async handleRunBatch() {
        try {
            if (!this.dom.state.uploaded) throw new Error("Спочатку завантажте файли.");

            const body = {
                runId: this.dom.state.uploaded.runId,
                inputFolder: this.dom.state.uploaded.inputFolder,
                engine: this.dom.$("batchEngine")?.value || config.defaults.engine,
                method: this.dom.$("batchMethod")?.value || config.defaults.method,
                strength: parseFloat(this.dom.$("batchStrength")?.value || config.defaults.strength),
                evaluate: !!this.dom.$("batchEvaluate")?.checked,
                ioUThreshold: config.defaults.ioUThreshold
            };

            if (body.method === "SolidColor") {
                body.colorHex = this.dom.$("batchColorPicker")?.value || config.defaults.color;
            }

            this.progress.show("Обробка файлів…");
            const progressConfig = config.progress.batch.process;
            this.progress.animate(
                "Пакетна обробка",
                progressConfig.softCeiling,
                progressConfig.intervalMs,
                progressConfig.speed
            );

            const json = await this.api.runBatch(body);
            await this.progress.finish();
            
            this.dom.setJson(json);
            this.kpiRenderer.renderBatchKpi(json);

            this.dom.state.lastBatchRunId = json.runId;
            await this.renderReports(json);

            // Завантажуємо списки результатів
            const [anonList, detectList] = await Promise.all([
                this.api.listOutputs(json.runId, "anon"),
                this.api.listOutputs(json.runId, "detect")
            ]);
            
            this.dom.state.batchAnonList = anonList;
            this.dom.state.batchDetectList = detectList;

            const list = this.currentList();
            if (list.length > 0) {
                this.showBatchImageByIndex(0);
            } else {
                this.showBatchImageByIndex(-1);
            }
        } catch (e) {
            this.progress.hide();
            this.dom.setJson({ error: e.message });
        }
    }

    async renderReports(json) {
        const reportsWrap = this.dom.$("batchReports");
        if (!reportsWrap) return;

        reportsWrap.innerHTML = "";

        const links = document.createElement("div");
        links.className = "kpi";
        links.appendChild(this.dom.createPill("Звіти:"));

        [
            { href: json.csvReport, label: "CSV" },
            { href: this.api.buildReportPdfUrl(json.runId, true), label: "PDF" },
            { href: this.api.buildDownloadZipUrl(json.runId), label: "ZIP" }
        ].forEach(({ href, label }) => {
            const a = document.createElement("a");
            a.href = href;
            a.textContent = label;
            a.target = "_blank";
            links.appendChild(a);
        });
        
        reportsWrap.appendChild(links);
    }

    currentList() {
        return this.dom.state.batchMode === "detect" 
            ? this.dom.state.batchDetectList 
            : this.dom.state.batchAnonList;
    }

    setBatchMode(mode) {
        this.dom.state.batchMode = mode === "detect" ? "detect" : "anon";
        this.dom.$("btnModeAnon")?.classList.toggle("is-active", this.dom.state.batchMode === "anon");
        this.dom.$("btnModeDetect")?.classList.toggle("is-active", this.dom.state.batchMode === "detect");
        this.showBatchImageByIndex(this.dom.state.batchIndex);
    }

    showBatchImageByIndex(idx) {
        const list = this.currentList();
        const img = this.dom.$("batchImg");
        const ph = this.dom.$("batchImgPlaceholder");
        const meta = this.dom.$("batchViewerMeta");
        const open = this.dom.$("batchOpenLink");
        const prev = this.dom.$("btnPrev");
        const next = this.dom.$("btnNext");

        if (!img || !ph || !meta || !open || !prev || !next) return;

        if (!list.length || !this.dom.state.lastBatchRunId) {
            this.dom.state.batchIndex = -1;
            img.classList.add("is-hidden");
            img.classList.remove("clickable");
            ph.classList.remove("is-hidden");
            ph.textContent = "Немає результатів. Запустіть пакетну обробку.";
            meta.textContent = "—";
            open.classList.add("is-hidden");
            prev.disabled = true;
            next.disabled = true;
            this.setBatchCounter();
            return;
        }

        this.dom.state.batchIndex = Math.max(0, Math.min(idx, list.length - 1));
        const it = list[this.dom.state.batchIndex];
        const rel = it.path ?? it.Path ?? it.relativePath ?? it.RelativePath ?? "";

        if (!rel) {
            img.classList.add("is-hidden");
            img.classList.remove("clickable");
            ph.classList.remove("is-hidden");
            ph.textContent = "Шлях не знайдено.";
            open.classList.add("is-hidden");
            meta.textContent = "—";
            return;
        }

        const url = this.api.buildOutputUrl(this.dom.state.lastBatchRunId, rel);
        img.src = url;
        img.classList.remove("is-hidden");
        img.classList.add("clickable");
        ph.classList.add("is-hidden");

        const name = it.name ?? it.Name ?? "файл";
        const size = it.sizeBytes ?? it.SizeBytes ?? "?";
        meta.textContent = `${name} · ${size} байт · ${this.dom.state.batchMode}`;
        open.href = url;
        open.classList.remove("is-hidden");

        prev.disabled = this.dom.state.batchIndex <= 0;
        next.disabled = this.dom.state.batchIndex >= list.length - 1;
        this.setBatchCounter();
    }

    setBatchCounter() {
        const list = this.currentList();
        const el = this.dom.$("batchCounter");
        if (el) {
            el.textContent = `${this.dom.state.batchIndex >= 0 ? this.dom.state.batchIndex + 1 : 0} / ${list.length}`;
        }
    }
}
