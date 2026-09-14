(function () {
    "use strict";

    const minimumOptionsForSearch = 7;
    const collator = new Intl.Collator("es", { sensitivity: "base", numeric: true });
    let generatedId = 0;

    function isPlaceholder(option, index) {
        const text = option.textContent.trim().toLocaleLowerCase("es");
        return index === 0 && (option.disabled || option.value === "" || option.value === "-1"
            || text.startsWith("seleccione"));
    }

    function isInitialControlOption(option, index) {
        return isPlaceholder(option, index)
            || (index === 0 && option.value.toLocaleUpperCase("es") === "TODOS");
    }

    function sortOptions(select) {
        const sort = container => {
            const options = Array.from(container.children).filter(item => item.tagName === "OPTION");
            const placeholder = options.length && isInitialControlOption(options[0], 0) ? options.shift() : null;
            const ordered = options.sort((left, right) =>
                collator.compare(left.textContent.trim(), right.textContent.trim()));
            const expected = placeholder ? [placeholder, ...ordered] : ordered;
            const current = Array.from(container.children).filter(item => item.tagName === "OPTION");
            if (expected.some((option, index) => option !== current[index])) {
                expected.forEach(option => container.appendChild(option));
            }
        };

        sort(select);
        Array.from(select.children)
            .filter(item => item.tagName === "OPTGROUP")
            .forEach(sort);
    }

    function enhance(select) {
        if (select.dataset.searchableSelectReady || select.multiple) return;

        select.dataset.searchableSelectReady = "true";
        select.id ||= `ordered-select-${++generatedId}`;

        const wrapper = document.createElement("div");
        wrapper.className = "searchable-select";
        if (select.classList.contains("form-select-lg")) wrapper.classList.add("searchable-select--lg");
        if (select.classList.contains("form-select-sm")) wrapper.classList.add("searchable-select--sm");

        const trigger = document.createElement("button");
        trigger.type = "button";
        trigger.className = "searchable-select__trigger form-select";
        trigger.setAttribute("aria-haspopup", "listbox");
        trigger.setAttribute("aria-expanded", "false");

        const panel = document.createElement("div");
        panel.className = "searchable-select__panel";
        panel.hidden = true;

        const search = document.createElement("input");
        search.type = "search";
        search.className = "searchable-select__search form-control";
        search.placeholder = "Buscar...";
        search.autocomplete = "off";
        search.setAttribute("aria-label", "Buscar una opción");

        const list = document.createElement("div");
        list.className = "searchable-select__options";
        list.id = `${select.id}-options`;
        list.setAttribute("role", "listbox");
        trigger.setAttribute("aria-controls", list.id);

        panel.append(search, list);
        select.before(wrapper);
        wrapper.append(trigger, panel, select);
        select.classList.add("searchable-select__native");

        function close() {
            panel.hidden = true;
            trigger.setAttribute("aria-expanded", "false");
            search.value = "";
        }

        function open() {
            if (select.disabled) return;
            document.querySelectorAll(".searchable-select__panel:not([hidden])").forEach(openPanel => {
                if (openPanel !== panel) openPanel.closest(".searchable-select").querySelector(".searchable-select__trigger").click();
            });
            panel.hidden = false;
            trigger.setAttribute("aria-expanded", "true");
            filterOptions("");
            if (!search.hidden) search.focus();
            else list.querySelector(".searchable-select__option:not(:disabled)")?.focus();
        }

        function filterOptions(query) {
            const normalized = query.trim().toLocaleLowerCase("es");
            let visible = 0;
            list.querySelectorAll(".searchable-select__option").forEach(option => {
                option.hidden = normalized !== "" && !option.textContent.toLocaleLowerCase("es").includes(normalized);
                if (!option.hidden) visible++;
            });
            list.querySelector(".searchable-select__empty").hidden = visible !== 0;
        }

        function render() {
            sortOptions(select);
            const options = Array.from(select.options);
            const selected = options.find(option => option.selected) || options[0];
            trigger.textContent = selected?.textContent.trim() || "Seleccione una opción";
            trigger.disabled = select.disabled;
            trigger.classList.toggle("searchable-select__placeholder", !selected || isPlaceholder(selected, options.indexOf(selected)));
            search.hidden = options.filter((option, index) => !isInitialControlOption(option, index)).length < minimumOptionsForSearch;
            list.replaceChildren();

            options.forEach((option, index) => {
                const item = document.createElement("button");
                item.type = "button";
                item.className = "searchable-select__option";
                item.textContent = option.textContent.trim();
                item.disabled = option.disabled;
                item.hidden = option.hidden;
                item.setAttribute("role", "option");
                item.setAttribute("aria-selected", option.selected ? "true" : "false");
                if (option.selected) item.classList.add("is-selected");
                if (isPlaceholder(option, index)) item.classList.add("searchable-select__option--placeholder");
                item.addEventListener("click", () => {
                    select.value = option.value;
                    select.dispatchEvent(new Event("change", { bubbles: true }));
                    close();
                    trigger.focus();
                });
                list.append(item);
            });

            const empty = document.createElement("p");
            empty.className = "searchable-select__empty";
            empty.textContent = "No se encontraron opciones";
            empty.hidden = true;
            list.append(empty);
        }

        trigger.addEventListener("click", () => panel.hidden ? open() : close());
        search.addEventListener("input", () => filterOptions(search.value));
        search.addEventListener("keydown", event => {
            if (event.key === "Enter") {
                event.preventDefault();
                event.stopPropagation();
                return;
            }

            if (event.key === "ArrowDown") {
                event.preventDefault();
                list.querySelector(".searchable-select__option:not([hidden]):not(:disabled)")?.focus();
            }
        });
        wrapper.addEventListener("keydown", event => {
            if (event.key === "Escape") { close(); trigger.focus(); }
        });
        select.addEventListener("change", render);
        select.addEventListener("focus", () => trigger.focus());
        select.addEventListener("invalid", () => { open(); setTimeout(() => trigger.focus()); });
        new MutationObserver(render).observe(select, { childList: true, subtree: true, attributes: true });
        render();
    }

    function initialize(root) {
        if (root.matches?.("select")) enhance(root);
        root.querySelectorAll?.("select").forEach(enhance);
    }

    document.addEventListener("click", event => {
        document.querySelectorAll(".searchable-select__panel:not([hidden])").forEach(panel => {
            if (!panel.parentElement.contains(event.target)) panel.parentElement.querySelector(".searchable-select__trigger").click();
        });
    });

    document.addEventListener("DOMContentLoaded", () => {
        initialize(document);
        new MutationObserver(mutations => mutations.forEach(mutation => mutation.addedNodes.forEach(node => {
            if (node.nodeType === Node.ELEMENT_NODE) initialize(node);
        }))).observe(document.body, { childList: true, subtree: true });
    });
})();
