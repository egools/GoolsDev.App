(function (global) {
    "use strict";

    function svgEl(tag, parent, attrs) {
        const el = document.createElementNS("http://www.w3.org/2000/svg", tag);
        for (const i in attrs || {}) el.setAttribute(i, attrs[i]);
        if (parent) parent.appendChild(el);
        return el;
    }

    function buildTooltip(container) {
        const tip = container.querySelector(".bc-tip");
        return {
            show(html, event) {
                tip.innerHTML = html;
                const rect = container.getBoundingClientRect();
                let x = event.clientX - rect.left + 14;
                const y = event.clientY - rect.top + 14;
                if (x + tip.offsetWidth > rect.width) x = event.clientX - rect.left - tip.offsetWidth - 14;
                tip.style.transform = `translate(${Math.max(0, x)}px, ${y}px)`;
                tip.style.opacity = 1;
            },
            hide() {
                tip.style.opacity = 0;
            }
        };
    }

    function roundChartMax(v, step) {
        return Math.ceil(v / step) * step;
    }

    // Push overlapping labels apart (items: [{y}]) -> sets item.ly
    function spreadLabels(items, gap, minY, maxY) {
        const s = items.slice().sort((a, b) => a.y - b.y);
        s.forEach((i) => {
            i.ly = i.y;
        });
        for (let iter = 0; iter < 100; iter++) {
            let moved = false;
            for (let k = 1; k < s.length; k++) {
                const d = s[k].ly - s[k - 1].ly;
                if (d < gap - 0.01) {
                    const p = (gap - d) / 2;
                    s[k - 1].ly -= p;
                    s[k].ly += p;
                    moved = true;
                }
            }
            if (s[0].ly < minY) {
                const sh = minY - s[0].ly;
                s.forEach((i) => {
                    i.ly += sh;
                });
                moved = true;
            }
            const last = s[s.length - 1];
            if (last.ly > maxY) {
                const sh = last.ly - maxY;
                s.forEach((i) => {
                    i.ly -= sh;
                });
                moved = true;
            }
            if (!moved) break;
        }
    }

    function renderPickemLeaderboard(container, data) {
        const Width = 800,
            Height = 550,
            margin = { top: 20, right: 120, bottom: 30, left: 50 };
        const weeks = data.weeks;
        const names = data.pickers;

        const maxTotal = Math.max(...names.map((n) => data.leaderboard[n][data.leaderboard[n].length - 1].total));
        const yMax = roundChartMax(maxTotal, 10);
        const innerW = Width - margin.left - margin.right,
            innerH = Height - margin.top - margin.bottom,
            pad = 20;

        const xFromWeek = (weekIndex) => margin.left + pad + (weeks.length === 1 ? 0 : (weekIndex * (innerW - 2 * pad)) / (weeks.length - 1));

        const yFromPoints = (points) => margin.top + (1 - points / yMax) * innerH;

        const chartContainer = container.querySelector(".pickem-chart-container");
        const svg = svgEl("svg", chartContainer, { viewBox: `0 0 ${Width} ${Height}`, role: "img" });
        const tip = buildTooltip(container);

        // grid + axes
        for (let points = 0; points <= yMax; points += 10) {
            svgEl("line", svg, { x1: margin.left, x2: Width - margin.right, y1: yFromPoints(points), y2: yFromPoints(points), class: "bc-grid" });
            const pointsLabel = svgEl("text", svg, { x: margin.left - 8, y: yFromPoints(points) + 4, "text-anchor": "end", class: "bc-axis" });
            pointsLabel.textContent = points;
        }

        //x axis
        weeks.forEach((week, weekIndex) => {
            const weekLabel = svgEl("text", svg,
                {
                    x: xFromWeek(weekIndex),
                    y: Height - margin.bottom + 20,
                    "text-anchor": "middle",
                    class: "bc-axis"
                });
            weekLabel.textContent = week;
        });

        //y axis
        const yAxisLabel = svgEl("text", svg, {
            transform: `translate(14 ${margin.top + innerH / 2}) rotate(-90)`,
            "text-anchor": "middle",
            class: "bc-title"
        });
        yAxisLabel.textContent = "Total points";

        // end-of-line labels (de-collided)
        const labelItems = names.map((n) => {
            const rows = data.leaderboard[n];
            return { name: n, y: yFromPoints(rows[rows.length - 1].total), total: rows[rows.length - 1].total };
        });

        spreadLabels(labelItems, 14.5, margin.top + 6, Height - margin.bottom - 2);
        const labelFor = {};
        labelItems.forEach((i) => {
            labelFor[i.name] = i;
        });

        // series
        const groups = {};
        function setActive(name) {
            names.forEach((n) => groups[n].classList.toggle("bc-dim", !!name && n !== name));
        }

        let nameList = names.slice().reverse();
        nameList.forEach((name) => {
            // draw top-ranked last so they sit on top
            const rows = data.leaderboard[name],
                color = `var(--${name}Color)`;
            const group = svgEl("g", svg, { class: "bc-fade" });
            groups[name] = group;

            const d = rows.map((r, i) => (i ? "L" : "M") + xFromWeek(i).toFixed(1) + "," + yFromPoints(r.total).toFixed(1)).join(" ");
            svgEl("path", group, { d, fill: "none", stroke: color, "stroke-width": 1.5, "stroke-linejoin": "round", "stroke-linecap": "round" });
            const hit = svgEl("path", group, { d, class: "bc-hit" });
            hit.addEventListener("mousemove", (ev) => tip.show(`<b>${name}</b><br>${rows[rows.length - 1].total} pts total`, ev));

            rows.forEach((r, i) => {
                const c = svgEl("circle", group, {
                    cx: xFromWeek(i),
                    cy: yFromPoints(r.total),
                    r: 3,
                    class: "bc-dot",
                    fill: r.missed ? "var(--bmgcDarkGrey)" : color,
                    stroke: color,
                    "stroke-width": 2
                });
                c.addEventListener("mousemove", (ev) =>
                    tip.show(
                        `<b>${name}</b> · Week ${r.week}<br>` + (r.missed ? "No picks<br>" : `+${r.points} pts this week<br>`) + `${r.total} pts total`,
                        ev
                    )
                );
            });

            // label + connector
            const pickerLabel = labelFor[name],
                lx = Width - margin.right + 22,
                ex = xFromWeek(rows.length - 1);
            svgEl("line", group, { x1: ex + 6, y1: pickerLabel.y, x2: lx - 8, y2: pickerLabel.ly, stroke: color, "stroke-width": 1, opacity: 0.5 });
            svgEl("circle", group, { cx: lx - 3, cy: pickerLabel.ly, r: 4.5, fill: color });
            const txt = svgEl("text", group, { x: lx + 7, y: pickerLabel.ly + 4.2, class: "bc-label" });
            txt.textContent = `${name} ${pickerLabel.total}`;

            group.addEventListener("mouseenter", () => setActive(name));
            group.addEventListener("mouseleave", () => {
                setActive(null);
                tip.hide();
            });
        });
        svg.addEventListener("mouseleave", () => tip.hide());
        // hide tooltip when moving off dots/lines but staying in the svg
        svg.addEventListener("mouseover", (ev) => {
            if (!ev.target.closest || !ev.target.closest(".bc-dot, .bc-hit")) {
                tip.hide();
            }
        });
    }

    function renderPickemYardage(container, data, legendEl) {
        const pickemYards = data.yardage,
            weeks = data.weeks;
        const Width = 700,
            Height = 550,
            margin = { top: 20, right: 20, bottom: 70, left: 50 };
        const pickers = data.pickers,
            pickerCount = pickers.length;
        const propByWeek = {};
        pickemYards.props.forEach((p) => {
            propByWeek[p.week] = p;
        });

        let maxYards = 0;
        pickemYards.props.forEach((p) => {
            maxYards = Math.max(maxYards, p.actual);
        });
        pickers.forEach((p) =>
            (pickemYards.guesses[p] || []).forEach((g) => {
                maxYards = Math.max(maxYards, g.yards);
            })
        );
        const yMax = roundChartMax(maxYards, 50);

        const innerW = Width - margin.left - margin.right,
            innerH = Height - margin.top - margin.bottom,
            weekWidth = innerW / weeks.length;
        // const laneStep = Math.min((weekWidth * 0.75) / (pickerCount - 1), 8);
        // const lane = (pickerIndex) => (pickerIndex - (pickerCount - 1) / 2) * laneStep;
        const xFromWeek = (weekIndex) => margin.left + (weekIndex + 0.5) * weekWidth;
        const yFromYards = (yards) => margin.top + (1 - yards / yMax) * innerH;

        const chartContainer = container.querySelector(".pickem-chart-container");
        const svg = svgEl("svg", chartContainer, { viewBox: `0 0 ${Width} ${Height}`, role: "img" });
        const tip = buildTooltip(container);

        // alternating week bands
        weeks.forEach((w, wi) => {
            if (wi % 2 === 0) {
                svgEl("rect", svg, { x: margin.left + wi * weekWidth, y: margin.top, width: weekWidth, height: innerH, class: "bc-band" });
            }
        });

        // grid + axes
        //x axis
        for (let yardage = 0; yardage <= yMax; yardage += 50) {
            const y = yFromYards(yardage);
            svgEl("line", svg, { x1: margin.left, x2: Width - margin.right, y1: y, y2: y, class: "bc-grid" });
            const t = svgEl("text", svg, { x: margin.left - 8, y: y + 4, "text-anchor": "end", class: "bc-axis" });
            t.textContent = yardage;
        }

        weeks.forEach((w, wi) => {
            const weekText = svgEl("text", svg,
                {
                    x: xFromWeek(wi),
                    y: Height - margin.bottom + 20,
                    "text-anchor": "middle",
                    class: "bc-axis"
                });
            weekText.textContent = `Week ${w}`;
            const propPlayerText = svgEl("text", svg,
                {
                    x: xFromWeek(wi),
                    y: Height - margin.bottom + 36,
                    "text-anchor": "middle",
                    class: "bc-axis-sub"
                });
            propPlayerText.textContent = propByWeek[w].players;

            const propStatText = svgEl("text", svg,
                {
                    x: xFromWeek(wi),
                    y: Height - margin.bottom + 52,
                    "text-anchor": "middle",
                    class: "bc-axis-sub"
                });
            propStatText.textContent = propByWeek[w].statType;
        });

        //y axis
        const yAxisLabel = svgEl("text", svg, {
            transform: `translate(10 ${margin.top + innerH / 2}) rotate(-90)`,
            "text-anchor": "middle",
            class: "bc-title"
        });
        yAxisLabel.textContent = "Yards";

        // hover / pin state shared by dots and legend
        let hover = null,
            pinned = null;
        const dots = []; // {el, name}
        const legendBtns = {}; // name -> button
        function refresh() {
            const active = hover || pinned;
            dots.forEach((d) => d.el.classList.toggle("bc-dim", !!active && d.name !== active));
            pickers.forEach((picker) => {
                const button = legendBtns[picker];
                if (!button) return;
                button.classList.toggle("is-active", active === picker);
                button.classList.toggle("is-dim", !!active && active !== picker);
            });
        }

        // answer reference line
        weeks.forEach((week, weekIndex) => {
            const prop = propByWeek[week];
            if (!prop) return;
            const half = weekWidth / 2 - 4
            svgEl("line", svg, {
                x1: xFromWeek(weekIndex) - half,
                x2: xFromWeek(weekIndex) + half,
                y1: yFromYards(prop.actual),
                y2: yFromYards(prop.actual),
                stroke: "currentColor",
                "stroke-width": 1.5,
                "stroke-dasharray": "3.5 3",
                opacity: 0.5
            });
            const actualYards = svgEl("text", svg, {
                x: xFromWeek(weekIndex) - half,
                y: yFromYards(prop.actual) - 2,
                class: "bc-yards-actual"
            });
            actualYards.textContent = prop.actual;
        });

        // picker dots
        pickers.forEach((picker, pickerIndex) => {
            (pickemYards.guesses[picker] || []).forEach((guess) => {
                const weekIndex = weeks.indexOf(guess.week);
                if (weekIndex < 0) return;
                const prop = propByWeek[guess.week];

                const win = prop && prop.winners.indexOf(picker) >= 0;
                const x = xFromWeek(weekIndex); //+ lane(pickerIndex),

                let dot, yardsStr, offStr;
                if (guess.isInfinite) {
                    yardsStr = "∞";
                    offStr = "(+∞)";
                    dot = svgEl("text", svg, {
                        x: x,
                        y: yFromYards(yMax) + 7,
                        fill: `var(--${picker}Color)`,
                        stroke: win ? "currentColor" : `var(--${picker}Color)`,
                        "stroke-width": 0,
                        "text-anchor": "middle",
                        class: "bc-dot bc-fade bc-infinite"
                    });
                    dot.textContent = "∞";
                }
                else {
                    yardsStr = guess.yards;
                    const off = guess.yards - prop.actual;
                    offStr = `(${off > 0 ? "+" : ""}${off})`;
                    dot = svgEl("circle", svg, {
                        cx: x,
                        cy: yFromYards(guess.yards),
                        r: win ? 5 : 4,
                        fill: `var(--${picker}Color)`,
                        stroke: win ? "currentColor" : `var(--${picker}Color)`,
                        "stroke-width": win ? 2 : 0,
                        class: "bc-dot bc-fade"
                    });
                }
                dot.addEventListener("mousemove", (ev) => {
                    tip.show(`<b>${picker}</b> · Week ${guess.week}<br>Guess: ${yardsStr} ${offStr}`, ev);
                    if (hover !== picker) {
                        hover = picker;
                        refresh();
                    }
                });
                dot.addEventListener("mouseleave", () => {
                    hover = null;
                    refresh();
                    tip.hide();
                });
                dot.addEventListener("click", () => {
                    pinned = pinned === picker ? null : picker;
                    refresh();
                });
                dots.push({ el: dot, name: picker });
            });
        });

        // legend
        const legend = document.querySelector(".bc-legend");
        pickers.forEach((picker) => {
            const btn = legend.querySelector(`#${picker}`);
            btn.addEventListener("mouseenter", () => {
                hover = picker;
                refresh();
            });
            btn.addEventListener("mouseleave", () => {
                hover = null;
                refresh();
            });
            btn.addEventListener("focus", () => {
                hover = picker;
                refresh();
            });
            btn.addEventListener("blur", () => {
                hover = null;
                refresh();
            });
            btn.addEventListener("click", () => {
                if (pinned === picker) {
                    pinned = null;
                    hover = null;
                }
                else
                    pinned = picker;
                refresh();
            });
            legendBtns[picker] = btn;
        });

        svg.addEventListener("mouseleave", () => tip.hide());
    }

    global.BaldyCharts = { renderPickemLeaderboard, renderPickemYardage };
})(window);
