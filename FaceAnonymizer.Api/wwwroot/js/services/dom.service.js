/**
 * Управління DOM елементами та UI
 */

export class DomManager {
    constructor() {
        this.state = {
            lastJson: {},
            uploaded: null,
            droppedFiles: [],
            batchAnonList: [],
            batchDetectList: [],
            batchIndex: -1,
            lastBatchRunId: "",
            batchMode: "anon"
        };
    }

    /**
     * Швидкий доступ до елементу за ID
     */
    $(id) {
        return document.getElementById(id);
    }

    /**
     * Відображення JSON в debug блоці
     */
    setJson(obj) {
        this.state.lastJson = obj || {};
        const out = this.$("out");
        if (out) out.textContent = JSON.stringify(this.state.lastJson, null, 2);
    }

    /**
     * Створення KPI пілюлі
     */
    createPill(text) {
        const el = document.createElement("div");
        el.className = "pill";
        el.textContent = text;
        return el;
    }

    /**
     * Відображення/приховування елементу
     */
    toggleElement(element, show) {
        if (!element) return;
        if (show) {
            element.classList.remove("is-hidden");
        } else {
            element.classList.add("is-hidden");
        }
    }

    /**
     * Встановлення активного табу
     */
    switchTab(name) {
        document.querySelectorAll(".tab").forEach(t => {
            t.classList.toggle("is-active", t.dataset.tab === name);
        });

        this.toggleElement(this.$("tab-single"), name === "single");
        this.toggleElement(this.$("tab-batch"), name === "batch");
        this.toggleElement(this.$("tab-debug"), name === "debug");
    }

    /**
     * Налаштування випадаючого списку
     */
    setSelectOptions(selectEl, items) {
        if (!selectEl) return;
        selectEl.innerHTML = "";
        (items || []).forEach(v => {
            const o = document.createElement("option");
            o.value = v;
            o.textContent = v;
            selectEl.appendChild(o);
        });
    }

    /**
     * Запобігання відкриттю файлів браузером при drag-drop
     */
    preventDefaultDragBehavior() {
        ["dragenter", "dragover", "dragleave", "drop"].forEach(evt => {
            document.addEventListener(evt, (e) => {
                e.preventDefault();
                e.stopPropagation();
            }, false);
        });
    }
}
