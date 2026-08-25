import React, {
    useState,
    useMemo
} from "react";
import {
    Plus,
    Trash2,
    GripVertical,
    ChevronDown,
    ChevronRight,
    Download,
    ArrowUp,
    ArrowDown,
    X,
    Layers,
    GitBranch,
    Grid3x3,
    Route,
    FileJson,
    Info
} from "lucide-react";

// ---------------------------------------------------------------------------
// ImageNGP modelling. PRISM's backbone. (POC-stage: this is just a starting point.)
// Everything here is editable.
// ---------------------------------------------------------------------------

const ROLES = ["REQUIRE", "FORBID", "SUPPORT", "IGNORE"];
const UNKNOWN_POLICIES = [
    "TREAT_AS_FAIL",
    "TREAT_AS_NEUTRAL",
    "SKIP_RULE",
    "PROPAGATE_UNKNOWN",
];
const RANKS = ["P1", "P2", "P3", "P4"];

let uid = 100;
const nextId = (prefix) => `${prefix}-${uid++}`;

const seedFeatures = [{
        id: "feat-human-presence",
        name: "human-presence",
        analyzer: "YOLO",
        type: "boolean",
        description: "Binary human-in-frame detection.",
        params: [{
            key: "confidence-bar",
            value: "0.60"
        }]
    },
    {
        id: "feat-presentation-mode",
        name: "presentation-mode",
        analyzer: "CLIP",
        type: "enum:on-model,ghost-mannequin,flat-lay,mannequin",
        description: "Replaces the old collapsed FALSE prompt.",
        params: [{
            key: "confidence-bar",
            value: "0.60"
        }]
    },
    {
        id: "feat-body-visible",
        name: "body-visible",
        analyzer: "CLIP",
        type: "enum:full,bust,detail,none",
        description: "Semantic body-region visibility.",
        params: [{
            key: "confidence-bar",
            value: "0.65"
        }]
    },
    {
        id: "feat-hero-orientation",
        name: "hero-orientation",
        analyzer: "CLIP",
        type: "enum:front,back,side-on,unknown",
        description: "Product-facing direction.",
        params: [{
            key: "confidence-bar",
            value: "0.60"
        }]
    },
    {
        id: "feat-product-color",
        name: "product-color",
        analyzer: "Colors",
        type: "enum",
        description: "Dedicated color analyzer (split out of CLIP).",
        params: [{
            key: "confidence-bar",
            value: "0.45"
        }]
    },
    {
        id: "feat-image-occupancy",
        name: "image-occupancy",
        analyzer: "Geometry",
        type: "float 0-1",
        description: "Fraction of frame the product fills.",
        params: []
    },
    {
        id: "feat-crop-tightness",
        name: "crop-tightness",
        analyzer: "Geometry",
        type: "float 0-1",
        description: "How tightly the subject is cropped.",
        params: []
    },
    {
        id: "feat-visual-type-likelihood",
        name: "visual-product-type-likelihood",
        analyzer: "CLIP (+ remote API planned)",
        type: "enum, scored",
        description: "Visual plausibility signal only, NOT the routing key. Supports/forbids based on plausibility, like 'unlikely to be pregnant if gender=male'.",
        params: [{
            key: "confidence-bar",
            value: "0.45"
        }]
    },
];

const seedPhenotypes = [{
        id: "phe-model-detail-closeup",
        name: "model-detail-closeup",
        description: "Tight crop, human present, bust only.",
        mappings: [{
                featureId: "feat-image-occupancy",
                role: "REQUIRE",
                unknownPolicy: "TREAT_AS_FAIL",
                params: [{
                    key: "min",
                    value: "0.95"
                }]
            },
            {
                featureId: "feat-crop-tightness",
                role: "REQUIRE",
                unknownPolicy: "TREAT_AS_FAIL",
                params: [{
                    key: "min",
                    value: "0.95"
                }]
            },
            {
                featureId: "feat-human-presence",
                role: "REQUIRE",
                unknownPolicy: "TREAT_AS_FAIL",
                params: []
            },
            {
                featureId: "feat-body-visible",
                role: "SUPPORT",
                unknownPolicy: "SKIP_RULE",
                params: [{
                    key: "value",
                    value: "bust"
                }]
            },
        ],
    },
    {
        id: "phe-front-on-model",
        name: "front-on-model",
        description: "Standard hero shot, front-facing, on a human model.",
        mappings: [{
                featureId: "feat-presentation-mode",
                role: "REQUIRE",
                unknownPolicy: "TREAT_AS_FAIL",
                params: [{
                    key: "value",
                    value: "on-model"
                }]
            },
            {
                featureId: "feat-hero-orientation",
                role: "REQUIRE",
                unknownPolicy: "TREAT_AS_FAIL",
                params: [{
                    key: "value",
                    value: "front"
                }]
            },
            {
                featureId: "feat-human-presence",
                role: "REQUIRE",
                unknownPolicy: "TREAT_AS_FAIL",
                params: []
            },
        ],
    },
];

const seedProductTypes = ["Trousers", "Sweater", "Skirt", "Cardigan", "Dress", "Jacket", "Towel"];
const seedBrands = ["Brand A", "Brand B", "Brand C"];

const seedDetSlots = ["det0", "det1", "det2", "det3", "det4", "det5", "det6", "det7"];

// Standard slot map: { "P1|det0": phenotypeId }
const seedStandardSlotMap = {
    "P1|det0": "phe-front-on-model",
    "P2|det7": "phe-model-detail-closeup",
};

const seedProfiles = [{
    id: "profile-standard",
    name: "Standard",
    isBase: true,
    deltas: {}
}, ];

const seedRouting = [{
    id: nextId("rt"),
    brand: "Brand A",
    productType: "Sweater",
    profile: "Standard"
}, ];

// ---------------------------------------------------------------------------

function Section({
    icon: Icon,
    title,
    subtitle,
    children,
    right
}) {
    return ( <
        div style = {
            {
                marginBottom: 28
            }
        } >
        <
        div style = {
            {
                display: "flex",
                alignItems: "flex-end",
                justifyContent: "space-between",
                marginBottom: 12,
                gap: 12,
                flexWrap: "wrap"
            }
        } >
        <
        div >
        <
        div style = {
            {
                display: "flex",
                alignItems: "center",
                gap: 8
            }
        } >
        <
        Icon size = {
            15
        }
        color = "var(--accent)" / >
        <
        h2 style = {
            {
                margin: 0,
                fontFamily: "var(--font-d)",
                fontSize: 15,
                fontWeight: 600,
                letterSpacing: ".02em",
                textTransform: "uppercase",
                color: "var(--ink)"
            }
        } > {
            title
        } < /h2> < /
        div > {
            subtitle && < p style = {
                {
                    margin: "4px 0 0",
                    fontSize: 12.5,
                    color: "var(--ink-faint)",
                    maxWidth: 62 + "ch"
                }
            } > {
                subtitle
            } < /p>} < /
            div > {
                right
            } <
            /div> {
            children
        } <
        /div>
    );
}

function IconBtn({
    onClick,
    title,
    children,
    danger,
    disabled
}) {
    return ( <
        button onClick = {
            onClick
        }
        title = {
            title
        }
        disabled = {
            disabled
        }
        style = {
            {
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                width: 26,
                height: 26,
                borderRadius: 7,
                cursor: disabled ? "default" : "pointer",
                border: "1px solid var(--line)",
                background: "var(--panel-2)",
                color: danger ? "var(--bad)" : "var(--ink-soft)",
                opacity: disabled ? 0.35 : 1,
                flexShrink: 0,
            }
        } > {
            children
        } <
        /button>
    );
}

function Chip({
    children,
    onClick,
    draggable,
    onDragStart,
    tone = "accent",
    small
}) {
    return ( <
        div draggable = {
            draggable
        }
        onDragStart = {
            onDragStart
        }
        onClick = {
            onClick
        }
        style = {
            {
                display: "inline-flex",
                alignItems: "center",
                gap: 6,
                fontFamily: "var(--font-m)",
                fontSize: small ? 10.5 : 11.5,
                padding: small ? "4px 8px" : "6px 10px",
                borderRadius: 999,
                border: `1px solid ${tone === "accent" ? "color-mix(in srgb, var(--accent) 45%, var(--line))" : "var(--line)"}`,
                background: "var(--panel-2)",
                color: "var(--ink)",
                cursor: draggable ? "grab" : onClick ? "pointer" : "default",
                userSelect: "none",
                whiteSpace: "nowrap",
            }
        } > {
            draggable && < GripVertical size = {
                11
            }
            color = "var(--ink-faint)" / >
        } {
            children
        } <
        /div>
    );
}

function Select({
    value,
    onChange,
    options,
    placeholder,
    style
}) {
    return ( <
        select value = {
            value ?? ""
        }
        onChange = {
            (e) => onChange(e.target.value)
        }
        style = {
            {
                font: "12px var(--font-b)",
                background: "var(--panel-2)",
                color: "var(--ink)",
                border: "1px solid var(--line)",
                borderRadius: 7,
                padding: "5px 8px",
                ...style,
            }
        } > {
            placeholder && < option value = "" > {
                placeholder
            } < /option>} {
            options.map((o) => ( <
                option key = {
                    o.value ?? o
                }
                value = {
                    o.value ?? o
                } > {
                    o.label ?? o
                } < /option>
            ))
        } <
        /select>
    );
}

function TextInput({
    value,
    onChange,
    placeholder,
    style
}) {
    return ( <
        input value = {
            value
        }
        onChange = {
            (e) => onChange(e.target.value)
        }
        placeholder = {
            placeholder
        }
        style = {
            {
                font: "12px var(--font-b)",
                background: "var(--panel-2)",
                color: "var(--ink)",
                border: "1px solid var(--line)",
                borderRadius: 7,
                padding: "5px 8px",
                ...style,
            }
        }
        />
    );
}

function ParamRows({
    params,
    onChange
}) {
    const update = (i, field, val) => {
        const next = params.map((p, idx) => (idx === i ? {
            ...p,
            [field]: val
        } : p));
        onChange(next);
    };
    const remove = (i) => onChange(params.filter((_, idx) => idx !== i));
    const add = () => onChange([...params, {
        key: "",
        value: ""
    }]);
    return ( <
        div style = {
            {
                display: "flex",
                flexDirection: "column",
                gap: 5,
                marginTop: 6
            }
        } > {
            params.map((p, i) => ( <
                div key = {
                    i
                }
                style = {
                    {
                        display: "flex",
                        gap: 5
                    }
                } >
                <
                TextInput value = {
                    p.key
                }
                onChange = {
                    (v) => update(i, "key", v)
                }
                placeholder = "param"
                style = {
                    {
                        width: 110
                    }
                }
                /> <
                TextInput value = {
                    p.value
                }
                onChange = {
                    (v) => update(i, "value", v)
                }
                placeholder = "value"
                style = {
                    {
                        flex: 1
                    }
                }
                /> <
                IconBtn onClick = {
                    () => remove(i)
                }
                title = "Remove parameter"
                danger > < X size = {
                    12
                }
                /></IconBtn >
                <
                /div>
            ))
        } <
        button onClick = {
            add
        }
        style = {
            ghostAddStyle
        } > < Plus size = {
            11
        }
        /> Add parameter</button >
        <
        /div>
    );
}

const ghostAddStyle = {
    display: "inline-flex",
    alignItems: "center",
    gap: 5,
    alignSelf: "flex-start",
    font: "11px var(--font-m)",
    color: "var(--accent)",
    background: "transparent",
    border: "1px dashed color-mix(in srgb, var(--accent) 50%, var(--line))",
    borderRadius: 7,
    padding: "4px 9px",
    cursor: "pointer",
};

// ---------------------------------------------------------------------------

export default function PrismMapper() {
    const [tab, setTab] = useState("features");
    const [features, setFeatures] = useState(seedFeatures);
    const [phenotypes, setPhenotypes] = useState(seedPhenotypes);
    const [activePhenotypeId, setActivePhenotypeId] = useState(seedPhenotypes[0].id);
    const [productTypes, setProductTypes] = useState(seedProductTypes);
    const [brands, setBrands] = useState(seedBrands);
    const [detSlots, setDetSlots] = useState(seedDetSlots);
    const [profiles, setProfiles] = useState(seedProfiles);
    const [activeProfileId, setActiveProfileId] = useState("profile-standard");
    const [standardSlotMap, setStandardSlotMap] = useState(seedStandardSlotMap);
    const [routing, setRouting] = useState(seedRouting);
    const [dragFeatureId, setDragFeatureId] = useState(null);
    const [dragPhenotypeId, setDragPhenotypeId] = useState(null);

    const activePhenotype = phenotypes.find((p) => p.id === activePhenotypeId);
    const activeProfile = profiles.find((p) => p.id === activeProfileId);

    // ---------- feature registry ----------
    const addFeature = () => setFeatures([...features, {
        id: nextId("feat"),
        name: "new-feature",
        analyzer: "",
        type: "",
        description: "",
        params: []
    }]);
    const updateFeature = (id, patch) => setFeatures(features.map((f) => (f.id === id ? {
        ...f,
        ...patch
    } : f)));
    const removeFeature = (id) => {
        setFeatures(features.filter((f) => f.id !== id));
        setPhenotypes(phenotypes.map((p) => ({
            ...p,
            mappings: p.mappings.filter((m) => m.featureId !== id)
        })));
    };

    // ---------- phenotypes ----------
    const addPhenotype = () => {
        const id = nextId("phe");
        setPhenotypes([...phenotypes, {
            id,
            name: "new-phenotype",
            description: "",
            mappings: []
        }]);
        setActivePhenotypeId(id);
    };
    const updatePhenotype = (id, patch) => setPhenotypes(phenotypes.map((p) => (p.id === id ? {
        ...p,
        ...patch
    } : p)));
    const removePhenotype = (id) => {
        setPhenotypes(phenotypes.filter((p) => p.id !== id));
        if (activePhenotypeId === id) setActivePhenotypeId(phenotypes.find((p) => p.id !== id)?.id ?? null);
    };
    const addMapping = (phenotypeId, featureId) => {
        if (!featureId) return;
        setPhenotypes(phenotypes.map((p) => {
            if (p.id !== phenotypeId) return p;
            if (p.mappings.some((m) => m.featureId === featureId)) return p; // one feature once
            return {
                ...p,
                mappings: [...p.mappings, {
                    featureId,
                    role: "SUPPORT",
                    unknownPolicy: "SKIP_RULE",
                    params: []
                }]
            };
        }));
    };
    const updateMapping = (phenotypeId, featureId, patch) => {
        setPhenotypes(phenotypes.map((p) => p.id !== phenotypeId ? p : {
            ...p,
            mappings: p.mappings.map((m) => m.featureId === featureId ? {
                ...m,
                ...patch
            } : m),
        }));
    };
    const removeMapping = (phenotypeId, featureId) => {
        setPhenotypes(phenotypes.map((p) => p.id !== phenotypeId ? p : {
            ...p,
            mappings: p.mappings.filter((m) => m.featureId !== featureId)
        }));
    };

    // ---------- slot map / profiles ----------
    const cellKey = (rank, slot) => `${rank}|${slot}`;
    const getCellValue = (rank, slot) => {
        const key = cellKey(rank, slot);
        if (activeProfile?.isBase) return standardSlotMap[key] ?? null;
        return activeProfile?.deltas?.[key] !== undefined ? activeProfile.deltas[key] : standardSlotMap[key] ?? null;
    };
    const isOverride = (rank, slot) => !activeProfile?.isBase && activeProfile?.deltas?.[cellKey(rank, slot)] !== undefined;
    const setCellValue = (rank, slot, phenotypeId) => {
        const key = cellKey(rank, slot);
        if (activeProfile?.isBase) {
            setStandardSlotMap({
                ...standardSlotMap,
                [key]: phenotypeId
            });
        } else {
            setProfiles(profiles.map((p) => p.id !== activeProfileId ? p : {
                ...p,
                deltas: {
                    ...p.deltas,
                    [key]: phenotypeId
                }
            }));
        }
    };
    const clearCell = (rank, slot) => {
        const key = cellKey(rank, slot);
        if (activeProfile?.isBase) {
            const next = {
                ...standardSlotMap
            };
            delete next[key];
            setStandardSlotMap(next);
        } else {
            setProfiles(profiles.map((p) => p.id !== activeProfileId ? p : (() => {
                const next = {
                    ...p.deltas
                };
                delete next[key];
                return {
                    ...p,
                    deltas: next
                };
            })()));
        }
    };
    const addProfile = () => {
        const name = `Profile ${profiles.length}`;
        const id = nextId("profile");
        setProfiles([...profiles, {
            id,
            name,
            isBase: false,
            deltas: {}
        }]);
        setActiveProfileId(id);
    };
    const removeProfile = (id) => {
        if (id === "profile-standard") return;
        setProfiles(profiles.filter((p) => p.id !== id));
        setRouting(routing.map((r) => r.profile === profiles.find((p) => p.id === id)?.name ? {
            ...r,
            profile: "Standard"
        } : r));
        if (activeProfileId === id) setActiveProfileId("profile-standard");
    };
    const addDetSlot = () => setDetSlots([...detSlots, `det${detSlots.length}`]);
    const removeDetSlot = (slot) => setDetSlots(detSlots.filter((s) => s !== slot));

    // ---------- routing ----------
    const addRoutingRule = () => setRouting([...routing, {
        id: nextId("rt"),
        brand: brands[0] ?? "",
        productType: productTypes[0] ?? "",
        profile: "Standard"
    }]);
    const updateRoutingRule = (id, patch) => setRouting(routing.map((r) => (r.id === id ? {
        ...r,
        ...patch
    } : r)));
    const removeRoutingRule = (id) => setRouting(routing.filter((r) => r.id !== id));
    const moveRoutingRule = (id, dir) => {
        const i = routing.findIndex((r) => r.id === id);
        const j = i + dir;
        if (j < 0 || j >= routing.length) return;
        const next = [...routing];
        [next[i], next[j]] = [next[j], next[i]];
        setRouting(next);
    };
    const addTag = (list, setList, val) => {
        if (val && !list.includes(val)) setList([...list, val]);
    };
    const removeTag = (list, setList, val) => setList(list.filter((v) => v !== val));

    // ---------- export ----------
    const exportData = useMemo(() => ({
        A_feature_registry: features.map(({
            id,
            name,
            analyzer,
            type,
            description,
            params
        }) => ({
            id,
            name,
            analyzer,
            type,
            description,
            parameters: Object.fromEntries(params.map((p) => [p.key, p.value]))
        })),
        B_phenotype_definitions: phenotypes.map((p) => ({
            id: p.id,
            name: p.name,
            description: p.description,
            features: p.mappings.map((m) => ({
                feature: features.find((f) => f.id === m.featureId)?.name ?? m.featureId,
                role: m.role,
                unknownPolicy: m.unknownPolicy,
                parameters: Object.fromEntries(m.params.map((pp) => [pp.key, pp.value])),
            })),
        })),
        C_standard_slot_map: RANKS.flatMap((rank) => detSlots.map((slot) => ({
            rank,
            slot,
            phenotype: standardSlotMap[cellKey(rank, slot)] ? phenotypes.find((p) => p.id === standardSlotMap[cellKey(rank, slot)])?.name : null
        }))).filter((r) => r.phenotype),
        D_routing_table: routing.map((r, i) => ({
            precedence: i + 1,
            brand: r.brand,
            productType: r.productType,
            profile: r.profile
        })),
        E_profile_deltas: profiles.filter((p) => !p.isBase).map((p) => ({
            profile: p.name,
            deltas: Object.entries(p.deltas).map(([key, phenotypeId]) => {
                const [rank, slot] = key.split("|");
                return {
                    rank,
                    slot,
                    phenotype: phenotypes.find((ph) => ph.id === phenotypeId)?.name ?? null
                };
            }),
        })),
        meta: {
            productTypes,
            brands,
            detSlots,
            ranks: RANKS
        },
    }), [features, phenotypes, standardSlotMap, routing, profiles, productTypes, brands, detSlots]);

    const downloadJson = () => {
        const blob = new Blob([JSON.stringify(exportData, null, 2)], {
            type: "application/json"
        });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = "prism-phenotype-config.json";
        a.click();
        URL.revokeObjectURL(url);
    };

    const TABS = [{
            id: "features",
            label: "Features",
            icon: Layers
        },
        {
            id: "phenotypes",
            label: "Phenotypes",
            icon: GitBranch
        },
        {
            id: "slotmap",
            label: "Slot Map & Profiles",
            icon: Grid3x3
        },
        {
            id: "routing",
            label: "Routing",
            icon: Route
        },
        {
            id: "export",
            label: "Export",
            icon: FileJson
        },
    ];

    return ( <
        div style = {
            {
                "--bg": "#0b0b0f",
                "--panel": "#0d0d10",
                "--panel-2": "#141419",
                "--line": "#2a2a32",
                "--ink": "#e6e6ea",
                "--ink-soft": "#a6a6b0",
                "--ink-faint": "#7a7a86",
                "--accent": "#3ac6c1",
                "--accent-2": "#8e68ff",
                "--bad": "#ff6f6f",
                "--font-d": "'Space Grotesk', system-ui, sans-serif",
                "--font-b": "'Inter', system-ui, sans-serif",
                "--font-m": "'JetBrains Mono', ui-monospace, monospace",
                background: "var(--bg)",
                color: "var(--ink)",
                minHeight: "100vh",
                fontFamily: "var(--font-b)",
                display: "flex",
                flexDirection: "column",
            }
        } >
        <
        style > {
            `
        @import url('https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@500;600;700&family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400;500;600&display=swap');
        * { box-sizing: border-box; }
        ::-webkit-scrollbar { height: 8px; width: 8px; }
        ::-webkit-scrollbar-thumb { background: #2a2a32; border-radius: 4px; }
        select, input, button { font-family: inherit; }
        button { font: inherit; }
      `
        } < /style>

        {
            /* top bar */
        } <
        div style = {
            {
                borderBottom: "1px solid var(--line)",
                background: "linear-gradient(180deg, rgba(13,13,16,.95), rgba(13,13,16,.7))",
                position: "sticky",
                top: 0,
                zIndex: 5
            }
        } >
        <
        div style = {
            {
                height: 3,
                background: "linear-gradient(90deg,#8e68ff,#3AA0FF,#2FD9C6,#e99c37)"
            }
        }
        /> <
        div style = {
            {
                maxWidth: 1180,
                margin: "0 auto",
                padding: "14px 20px",
                display: "flex",
                alignItems: "center",
                gap: 20,
                flexWrap: "wrap"
            }
        } >
        <
        div style = {
            {
                display: "flex",
                alignItems: "baseline",
                gap: 8,
                flex: "0 0 auto"
            }
        } >
        <
        span style = {
            {
                fontFamily: "var(--font-d)",
                fontWeight: 700,
                letterSpacing: ".12em",
                fontSize: 16
            }
        } > PRISM < /span> <
        span style = {
            {
                fontFamily: "var(--font-m)",
                fontSize: 10.5,
                color: "var(--ink-faint)"
            }
        } > phenotype & amp; ordering mapper < /span> < /
        div > <
        div style = {
            {
                display: "flex",
                gap: 6,
                marginLeft: "auto",
                flexWrap: "wrap"
            }
        } > {
            TABS.map((t) => ( <
                button key = {
                    t.id
                }
                onClick = {
                    () => setTab(t.id)
                }
                style = {
                    {
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 7,
                        padding: "7px 12px",
                        borderRadius: 999,
                        cursor: "pointer",
                        border: `1px solid ${tab === t.id ? "color-mix(in srgb, var(--accent) 60%, var(--line))" : "var(--line)"}`,
                        background: tab === t.id ? "var(--panel-2)" : "var(--panel)",
                        color: tab === t.id ? "var(--ink)" : "var(--ink-soft)",
                        fontSize: 12.5,
                        fontWeight: 500,
                    }
                } >
                <
                t.icon size = {
                    13
                }
                color = {
                    tab === t.id ? "var(--accent)" : "var(--ink-faint)"
                }
                /> {t.label} < /
                button >
            ))
        } <
        /div> < /
        div > <
        /div>

        <
        div style = {
            {
                maxWidth: 1180,
                margin: "0 auto",
                padding: "24px 20px 60px",
                width: "100%",
                flex: 1
            }
        } >

        {
            /* ================= FEATURES ================= */
        } {
            tab === "features" && ( <
                Section icon = {
                    Layers
                }
                title = "A · Feature registry"
                subtitle = "One feature = one analyzer, no overlap. Product type and brand deliberately live in Routing. Keys =/= visual traits."
                right = {
                    <
                    button onClick = {
                        addFeature
                    }
                    style = {
                        ghostAddStyle
                    } > < Plus size = {
                        12
                    }
                    /> Add feature</button >
                } >
                <
                div style = {
                    {
                        display: "flex",
                        flexDirection: "column",
                        gap: 8
                    }
                } > {
                    features.map((f) => ( <
                        div key = {
                            f.id
                        }
                        style = {
                            {
                                border: "1px solid var(--line)",
                                borderRadius: 10,
                                background: "var(--panel-2)",
                                padding: 12
                            }
                        } >
                        <
                        div style = {
                            {
                                display: "flex",
                                gap: 8,
                                flexWrap: "wrap",
                                alignItems: "center"
                            }
                        } >
                        <
                        TextInput value = {
                            f.name
                        }
                        onChange = {
                            (v) => updateFeature(f.id, {
                                name: v
                            })
                        }
                        placeholder = "feature-name"
                        style = {
                            {
                                width: 220,
                                fontFamily: "var(--font-m)"
                            }
                        }
                        /> <
                        TextInput value = {
                            f.analyzer
                        }
                        onChange = {
                            (v) => updateFeature(f.id, {
                                analyzer: v
                            })
                        }
                        placeholder = "analyzer (e.g. CLIP, YOLO)"
                        style = {
                            {
                                width: 180
                            }
                        }
                        /> <
                        TextInput value = {
                            f.type
                        }
                        onChange = {
                            (v) => updateFeature(f.id, {
                                type: v
                            })
                        }
                        placeholder = "value type"
                        style = {
                            {
                                width: 160
                            }
                        }
                        /> <
                        div style = {
                            {
                                marginLeft: "auto"
                            }
                        } > < IconBtn onClick = {
                            () => removeFeature(f.id)
                        }
                        title = "Remove feature"
                        danger > < Trash2 size = {
                            13
                        }
                        /></IconBtn > < /div> < /
                        div > <
                        TextInput value = {
                            f.description
                        }
                        onChange = {
                            (v) => updateFeature(f.id, {
                                description: v
                            })
                        }
                        placeholder = "description"
                        style = {
                            {
                                width: "100%",
                                marginTop: 8
                            }
                        }
                        /> <
                        details style = {
                            {
                                marginTop: 8
                            }
                        } >
                        <
                        summary style = {
                            {
                                cursor: "pointer",
                                fontSize: 11,
                                color: "var(--ink-faint)",
                                fontFamily: "var(--font-m)"
                            }
                        } > parameters({
                            f.params.length
                        }) < /summary> <
                        ParamRows params = {
                            f.params
                        }
                        onChange = {
                            (params) => updateFeature(f.id, {
                                params
                            })
                        }
                        /> < /
                        details > <
                        /div>
                    ))
                } <
                /div> < /
                Section >
            )
        }

        {
            /* ================= PHENOTYPES ================= */
        } {
            tab === "phenotypes" && ( <
                    div style = {
                        {
                            display: "grid",
                            gridTemplateColumns: "220px 1fr",
                            gap: 22
                        }
                    } >
                    <
                    div >
                    <
                    div style = {
                        {
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "space-between",
                            marginBottom: 10
                        }
                    } >
                    <
                    span style = {
                        {
                            fontFamily: "var(--font-m)",
                            fontSize: 10.5,
                            color: "var(--ink-faint)",
                            textTransform: "uppercase",
                            letterSpacing: ".08em"
                        }
                    } > Phenotypes < /span> <
                    IconBtn onClick = {
                        addPhenotype
                    }
                    title = "New phenotype" > < Plus size = {
                        13
                    }
                    /></IconBtn >
                    <
                    /div> <
                    div style = {
                        {
                            display: "flex",
                            flexDirection: "column",
                            gap: 6
                        }
                    } > {
                        phenotypes.map((p) => ( <
                            button key = {
                                p.id
                            }
                            onClick = {
                                () => setActivePhenotypeId(p.id)
                            }
                            style = {
                                {
                                    textAlign: "left",
                                    padding: "8px 10px",
                                    borderRadius: 8,
                                    cursor: "pointer",
                                    border: `1px solid ${activePhenotypeId === p.id ? "color-mix(in srgb, var(--accent) 55%, var(--line))" : "var(--line)"}`,
                                    background: activePhenotypeId === p.id ? "var(--panel-2)" : "transparent",
                                    color: "var(--ink)",
                                    fontSize: 12.5,
                                    fontFamily: "var(--font-m)",
                                }
                            } > {
                                p.name
                            } < /button>
                        ))
                    } <
                    /div>

                    <
                    div style = {
                        {
                            marginTop: 20
                        }
                    } >
                    <
                    span style = {
                        {
                            fontFamily: "var(--font-m)",
                            fontSize: 10.5,
                            color: "var(--ink-faint)",
                            textTransform: "uppercase",
                            letterSpacing: ".08em"
                        }
                    } > Drag a feature in → < /span> <
                    div style = {
                        {
                            display: "flex",
                            flexDirection: "column",
                            gap: 6,
                            marginTop: 8
                        }
                    } > {
                        features.map((f) => ( <
                            Chip key = {
                                f.id
                            }
                            draggable small onDragStart = {
                                (e) => {
                                    e.dataTransfer.setData("text/feature", f.id);
                                    setDragFeatureId(f.id);
                                }
                            } > {
                                f.name
                            } <
                            /Chip>
                        ))
                    } <
                    /div> < /
                    div > <
                    /div>

                    {
                        activePhenotype ? ( <
                            div >
                            <
                            div style = {
                                {
                                    display: "flex",
                                    gap: 10,
                                    alignItems: "center",
                                    flexWrap: "wrap",
                                    marginBottom: 6
                                }
                            } >
                            <
                            TextInput value = {
                                activePhenotype.name
                            }
                            onChange = {
                                (v) => updatePhenotype(activePhenotype.id, {
                                    name: v
                                })
                            }
                            style = {
                                {
                                    fontFamily: "var(--font-d)",
                                    fontSize: 16,
                                    fontWeight: 600,
                                    width: 320,
                                    background: "transparent",
                                    border: "none",
                                    padding: "2px 0"
                                }
                            }
                            /> <
                            div style = {
                                {
                                    marginLeft: "auto"
                                }
                            } >
                            <
                            IconBtn onClick = {
                                () => removePhenotype(activePhenotype.id)
                            }
                            title = "Delete phenotype"
                            danger > < Trash2 size = {
                                13
                            }
                            /></IconBtn >
                            <
                            /div> < /
                            div > <
                            TextInput value = {
                                activePhenotype.description
                            }
                            onChange = {
                                (v) => updatePhenotype(activePhenotype.id, {
                                    description: v
                                })
                            }
                            placeholder = "description"
                            style = {
                                {
                                    width: "100%",
                                    marginBottom: 14
                                }
                            }
                            />

                            {
                                /* drop zone */
                            } <
                            div onDragOver = {
                                (e) => e.preventDefault()
                            }
                            onDrop = {
                                (e) => {
                                    const fid = e.dataTransfer.getData("text/feature");
                                    addMapping(activePhenotype.id, fid);
                                    setDragFeatureId(null);
                                }
                            }
                            style = {
                                {
                                    border: `1px dashed ${dragFeatureId ? "var(--accent)" : "var(--line)"}`,
                                    borderRadius: 10,
                                    padding: 14,
                                    marginBottom: 14,
                                    background: "var(--panel-2)",
                                }
                            } >
                            <
                            div style = {
                                {
                                    display: "flex",
                                    alignItems: "center",
                                    gap: 8,
                                    marginBottom: activePhenotype.mappings.length ? 12 : 0,
                                    flexWrap: "wrap"
                                }
                            } >
                            <
                            span style = {
                                {
                                    fontSize: 11,
                                    color: "var(--ink-faint)",
                                    fontFamily: "var(--font-m)"
                                }
                            } > drop zone— or pick a feature: < /span> <
                            Select value = ""
                            onChange = {
                                (v) => addMapping(activePhenotype.id, v)
                            }
                            placeholder = "+ add feature"
                            options = {
                                features.filter((f) => !activePhenotype.mappings.some((m) => m.featureId === f.id)).map((f) => ({
                                    value: f.id,
                                    label: f.name
                                }))
                            }
                            /> < /
                            div >

                            <
                            div style = {
                                {
                                    display: "flex",
                                    flexDirection: "column",
                                    gap: 8
                                }
                            } > {
                                activePhenotype.mappings.map((m) => {
                                    const feat = features.find((f) => f.id === m.featureId);
                                    return ( <
                                        div key = {
                                            m.featureId
                                        }
                                        style = {
                                            {
                                                border: "1px solid var(--line)",
                                                borderRadius: 9,
                                                background: "var(--panel)",
                                                padding: 10
                                            }
                                        } >
                                        <
                                        div style = {
                                            {
                                                display: "flex",
                                                gap: 8,
                                                alignItems: "center",
                                                flexWrap: "wrap"
                                            }
                                        } >
                                        <
                                        span style = {
                                            {
                                                fontFamily: "var(--font-m)",
                                                fontSize: 12,
                                                color: "var(--accent)"
                                            }
                                        } > {
                                            feat?.name ?? m.featureId
                                        } < /span> <
                                        Select value = {
                                            m.role
                                        }
                                        onChange = {
                                            (v) => updateMapping(activePhenotype.id, m.featureId, {
                                                role: v
                                            })
                                        }
                                        options = {
                                            ROLES
                                        }
                                        /> <
                                        Select value = {
                                            m.unknownPolicy
                                        }
                                        onChange = {
                                            (v) => updateMapping(activePhenotype.id, m.featureId, {
                                                unknownPolicy: v
                                            })
                                        }
                                        options = {
                                            UNKNOWN_POLICIES.map((u) => ({
                                                value: u,
                                                label: `UNKNOWN → ${u}`
                                            }))
                                        }
                                        style = {
                                            {
                                                minWidth: 190
                                            }
                                        }
                                        /> <
                                        div style = {
                                            {
                                                marginLeft: "auto"
                                            }
                                        } >
                                        <
                                        IconBtn onClick = {
                                            () => removeMapping(activePhenotype.id, m.featureId)
                                        }
                                        title = "Remove"
                                        danger > < X size = {
                                            12
                                        }
                                        /></IconBtn >
                                        <
                                        /div> < /
                                        div > <
                                        details style = {
                                            {
                                                marginTop: 6
                                            }
                                        } >
                                        <
                                        summary style = {
                                            {
                                                cursor: "pointer",
                                                fontSize: 10.5,
                                                color: "var(--ink-faint)",
                                                fontFamily: "var(--font-m)"
                                            }
                                        } > parameters({
                                            m.params.length
                                        }) < /summary> <
                                        ParamRows params = {
                                            m.params
                                        }
                                        onChange = {
                                            (params) => updateMapping(activePhenotype.id, m.featureId, {
                                                params
                                            })
                                        }
                                        /> < /
                                        details > <
                                        /div>
                                    );
                                })
                            } {
                                !activePhenotype.mappings.length && < div style = {
                                    {
                                        fontSize: 12,
                                        color: "var(--ink-faint)"
                                    }
                                } > No features mapped yet. < /div>} < /
                                div > <
                                    /div> < /
                                div >
                            ): < div style = {
                                {
                                    color: "var(--ink-faint)",
                                    fontSize: 13
                                }
                            } > No phenotype selected. < /div>} < /
                            div >
                        )
                    }

                    {
                        /* ================= SLOT MAP & PROFILES ================= */
                    } {
                        tab === "slotmap" && ( <
                                div >
                                <
                                Section icon = {
                                    Grid3x3
                                }
                                title = "C + E · Standard slot map & profile deltas"
                                subtitle = "Rows = priority rank (P1 highest). Columns = det slots. Drag a phenotype onto a cell, or use the dropdown. Editing a non-Standard profile only stores the cells that differ."
                                right = {
                                    <
                                    div style = {
                                        {
                                            display: "flex",
                                            gap: 8,
                                            alignItems: "center",
                                            flexWrap: "wrap"
                                        }
                                    } >
                                    <
                                    Select value = {
                                        activeProfileId
                                    }
                                    onChange = {
                                        setActiveProfileId
                                    }
                                    options = {
                                        profiles.map((p) => ({
                                            value: p.id,
                                            label: p.isBase ? `${p.name} (base)` : p.name
                                        }))
                                    }
                                    /> {!activeProfile?.isBase && < IconBtn onClick = {
                                    () => removeProfile(activeProfileId)
                                }
                                title = "Delete profile"
                                danger > < Trash2 size = {
                                    13
                                }
                                /></IconBtn >
                            } <
                            button onClick = {
                                addProfile
                            }
                        style = {
                            ghostAddStyle
                        } > < Plus size = {
                            11
                        }
                        /> Profile</button >
                        <
                        button onClick = {
                            addDetSlot
                        }
                        style = {
                            ghostAddStyle
                        } > < Plus size = {
                            11
                        }
                        /> Det slot</button >
                        <
                        /div>
                    } >

                    <
                    div style = {
                        {
                            display: "flex",
                            gap: 18
                        }
                    } >
                    <
                    div style = {
                        {
                            width: 190,
                            flexShrink: 0
                        }
                    } >
                    <
                    span style = {
                        {
                            fontFamily: "var(--font-m)",
                            fontSize: 10.5,
                            color: "var(--ink-faint)",
                            textTransform: "uppercase",
                            letterSpacing: ".08em"
                        }
                    } > Phenotypes→ < /span> <
                    div style = {
                        {
                            display: "flex",
                            flexDirection: "column",
                            gap: 6,
                            marginTop: 8
                        }
                    } > {
                        phenotypes.map((p) => ( <
                            Chip key = {
                                p.id
                            }
                            draggable small tone = "accent"
                            onDragStart = {
                                (e) => {
                                    e.dataTransfer.setData("text/phenotype", p.id);
                                    setDragPhenotypeId(p.id);
                                }
                            } > {
                                p.name
                            } < /Chip>
                        ))
                    } <
                    /div> < /
                    div >

                    <
                    div style = {
                        {
                            overflowX: "auto",
                            flex: 1
                        }
                    } >
                    <
                    table style = {
                        {
                            borderCollapse: "collapse",
                            width: "100%",
                            minWidth: 480
                        }
                    } >
                    <
                    thead >
                    <
                    tr >
                    <
                    th style = {
                        thStyle
                    } > < /th> {
                    detSlots.map((slot) => ( <
                        th key = {
                            slot
                        }
                        style = {
                            thStyle
                        } >
                        <
                        div style = {
                            {
                                display: "flex",
                                alignItems: "center",
                                gap: 4,
                                justifyContent: "center"
                            }
                        } >
                        <
                        span style = {
                            {
                                fontFamily: "var(--font-m)"
                            }
                        } > {
                            slot
                        } < /span> <
                        span onClick = {
                            () => removeDetSlot(slot)
                        }
                        style = {
                            {
                                cursor: "pointer",
                                color: "var(--ink-faint)"
                            }
                        } > < X size = {
                            10
                        }
                        /></span >
                        <
                        /div> < /
                        th >
                    ))
                } <
                /tr> < /
            thead > <
                tbody > {
                    RANKS.map((rank) => ( <
                            tr key = {
                                rank
                            } >
                            <
                            td style = {
                                {
                                    ...tdStyle,
                                    fontFamily: "var(--font-m)",
                                    color: "var(--ink-faint)"
                                }
                            } > {
                                rank
                            } < /td> {
                            detSlots.map((slot) => {
                                const val = getCellValue(rank, slot);
                                const phe = phenotypes.find((p) => p.id === val);
                                const overridden = isOverride(rank, slot);
                                return ( <
                                    td key = {
                                        slot
                                    }
                                    style = {
                                        tdStyle
                                    } >
                                    <
                                    div onDragOver = {
                                        (e) => e.preventDefault()
                                    }
                                    onDrop = {
                                        (e) => {
                                            const pid = e.dataTransfer.getData("text/phenotype");
                                            if (pid) setCellValue(rank, slot, pid);
                                            setDragPhenotypeId(null);
                                        }
                                    }
                                    style = {
                                        {
                                            minHeight: 44,
                                            borderRadius: 8,
                                            border: `1px dashed ${dragPhenotypeId ? "var(--accent)" : "var(--line)"}`,
                                            display: "flex",
                                            flexDirection: "column",
                                            alignItems: "center",
                                            justifyContent: "center",
                                            gap: 4,
                                            padding: 4,
                                            background: overridden ? "color-mix(in srgb, var(--accent-2) 12%, var(--panel-2))" : "var(--panel-2)",
                                        }
                                    } > {
                                        phe ? ( <
                                            >
                                            <
                                            span style = {
                                                {
                                                    fontSize: 10.5,
                                                    fontFamily: "var(--font-m)",
                                                    textAlign: "center"
                                                }
                                            } > {
                                                phe.name
                                            } < /span> <
                                            span onClick = {
                                                () => clearCell(rank, slot)
                                            }
                                            style = {
                                                {
                                                    cursor: "pointer",
                                                    color: "var(--ink-faint)"
                                                }
                                            } > < X size = {
                                                10
                                            }
                                            /></span >
                                            <
                                            />
                                        ) : ( <
                                            Select value = ""
                                            onChange = {
                                                (v) => setCellValue(rank, slot, v)
                                            }
                                            placeholder = "—"
                                            options = {
                                                phenotypes.map((p) => ({
                                                    value: p.id,
                                                    label: p.name
                                                }))
                                            }
                                            style = {
                                                {
                                                    fontSize: 10,
                                                    padding: "3px 4px",
                                                    width: "100%"
                                                }
                                            }
                                            />
                                        )
                                    } <
                                    /div> < /
                                    td >
                                );
                            })
                        } <
                        /tr>
                    ))
        } <
        /tbody> < /
        table > {
            !activeProfile?.isBase && < p style = {
                {
                    fontSize: 11,
                    color: "var(--ink-faint)",
                    marginTop: 8
                }
            } > < span style = {
                {
                    display: "inline-block",
                    width: 8,
                    height: 8,
                    background: "var(--accent-2)",
                    borderRadius: 2,
                    marginRight: 6,
                    opacity: .5
                }
            }
            />tinted cells are overrides for <b style={{ color: "var(--ink)" }}>{activeProfile?.name}</b > ;
            blank cells inherit from Standard. < /p>} < /
            div > <
            /div> < /
            Section > <
            /div>
        )
    }

    {
        /* ================= ROUTING ================= */
    } {
        tab === "routing" && ( <
            div >
            <
            Section icon = {
                Route
            }
            title = "D · Routing table"
            subtitle = "Brand and product type as routing keys via the Internal Excel Model."
            right = {
                <
                button onClick = {
                    addRoutingRule
                }
                style = {
                    ghostAddStyle
                } > < Plus size = {
                    12
                }
                /> Add rule</button >
            } >
            <
            div style = {
                {
                    display: "flex",
                    flexDirection: "column",
                    gap: 6
                }
            } > {
                routing.map((r, i) => ( <
                    div key = {
                        r.id
                    }
                    style = {
                        {
                            display: "flex",
                            gap: 8,
                            alignItems: "center",
                            border: "1px solid var(--line)",
                            borderRadius: 9,
                            padding: "8px 10px",
                            background: "var(--panel-2)"
                        }
                    } >
                    <
                    span style = {
                        {
                            fontFamily: "var(--font-m)",
                            fontSize: 11,
                            color: "var(--ink-faint)",
                            width: 20
                        }
                    } > {
                        i + 1
                    } < /span> <
                    Select value = {
                        r.brand
                    }
                    onChange = {
                        (v) => updateRoutingRule(r.id, {
                            brand: v
                        })
                    }
                    options = {
                        brands
                    }
                    style = {
                        {
                            width: 130
                        }
                    }
                    /> <
                    Select value = {
                        r.productType
                    }
                    onChange = {
                        (v) => updateRoutingRule(r.id, {
                            productType: v
                        })
                    }
                    options = {
                        productTypes
                    }
                    style = {
                        {
                            width: 130
                        }
                    }
                    /> <
                    span style = {
                        {
                            color: "var(--ink-faint)"
                        }
                    } > → < /span> <
                    Select value = {
                        r.profile
                    }
                    onChange = {
                        (v) => updateRoutingRule(r.id, {
                            profile: v
                        })
                    }
                    options = {
                        profiles.map((p) => p.name)
                    }
                    style = {
                        {
                            width: 150
                        }
                    }
                    /> <
                    div style = {
                        {
                            marginLeft: "auto",
                            display: "flex",
                            gap: 4
                        }
                    } >
                    <
                    IconBtn onClick = {
                        () => moveRoutingRule(r.id, -1)
                    }
                    title = "Higher precedence"
                    disabled = {
                        i === 0
                    } > < ArrowUp size = {
                        12
                    }
                    /></IconBtn >
                    <
                    IconBtn onClick = {
                        () => moveRoutingRule(r.id, 1)
                    }
                    title = "Lower precedence"
                    disabled = {
                        i === routing.length - 1
                    } > < ArrowDown size = {
                        12
                    }
                    /></IconBtn >
                    <
                    IconBtn onClick = {
                        () => removeRoutingRule(r.id)
                    }
                    title = "Remove"
                    danger > < Trash2 size = {
                        13
                    }
                    /></IconBtn >
                    <
                    /div> < /
                    div >
                ))
            } {
                !routing.length && < div style = {
                    {
                        fontSize: 12,
                        color: "var(--ink-faint)"
                    }
                } > No rules yet. < /div>} < /
                div > <
                    /Section>

                    <
                    div style = {
                        {
                            display: "grid",
                            gridTemplateColumns: "1fr 1fr",
                            gap: 22,
                            marginTop: 26
                        }
                    } >
                    <
                    TagListEditor title = "Product types"
                items = {
                    productTypes
                }
                onAdd = {
                    (v) => addTag(productTypes, setProductTypes, v)
                }
                onRemove = {
                    (v) => removeTag(productTypes, setProductTypes, v)
                }
                note = "PoC list (can expand based on the NGP.)" / >
                    <
                    TagListEditor title = "Brands"
                items = {
                    brands
                }
                onAdd = {
                    (v) => addTag(brands, setBrands, v)
                }
                onRemove = {
                    (v) => removeTag(brands, setBrands, v)
                }
                /> < /
                div > <
                    /div>
            )
        }

        {
            /* ================= EXPORT ================= */
        } {
            tab === "export" && ( <
                Section icon = {
                    FileJson
                }
                title = "Export"
                subtitle = "Serializes the visual state back into the five-table config: feature registry, phenotype definitions, standard slot map, routing table, and profile deltas."
                right = {
                    <
                    button onClick = {
                        downloadJson
                    }
                    style = {
                        {
                            ...ghostAddStyle,
                            borderStyle: "solid"
                        }
                    } > < Download size = {
                        12
                    }
                    /> Download JSON</button >
                } >
                <
                pre style = {
                    {
                        background: "var(--panel-2)",
                        border: "1px solid var(--line)",
                        borderRadius: 10,
                        padding: 16,
                        fontSize: 11.5,
                        fontFamily: "var(--font-m)",
                        color: "var(--ink-soft)",
                        overflowX: "auto",
                        maxHeight: 560,
                        overflowY: "auto",
                    }
                } > {
                    JSON.stringify(exportData, null, 2)
                } < /pre> < /
                Section >
            )
        } <
        /div> < /
        div >
    );
}

function TagListEditor({
    title,
    items,
    onAdd,
    onRemove,
    note
}) {
    const [val, setVal] = useState("");
    return ( <
        div >
        <
        span style = {
            {
                fontFamily: "var(--font-m)",
                fontSize: 10.5,
                color: "var(--ink-faint)",
                textTransform: "uppercase",
                letterSpacing: ".08em"
            }
        } > {
            title
        } < /span> {
        note && < p style = {
            {
                fontSize: 11,
                color: "var(--ink-faint)",
                margin: "4px 0 8px"
            }
        } > {
            note
        } < /p>} <
        div style = {
            {
                display: "flex",
                gap: 6,
                flexWrap: "wrap",
                marginTop: 8,
                marginBottom: 10
            }
        } > {
            items.map((it) => ( <
                Chip key = {
                    it
                }
                small > {
                    it
                } < span onClick = {
                    () => onRemove(it)
                }
                style = {
                    {
                        cursor: "pointer",
                        marginLeft: 2,
                        color: "var(--ink-faint)"
                    }
                } > < X size = {
                    10
                }
                /></span >
                <
                /Chip>
            ))
        } <
        /div> <
        div style = {
            {
                display: "flex",
                gap: 6
            }
        } >
        <
        TextInput value = {
            val
        }
        onChange = {
            setVal
        }
        placeholder = {
            `add to ${title.toLowerCase()}`
        }
        style = {
            {
                flex: 1
            }
            /> <
            IconBtn onClick = {
                () => {
                    onAdd(val);
                    setVal("");
                }
            }
            title = "Add" > < Plus size = {
                13
            }
            /></IconBtn >
            <
            /div> </div >
        );
    }

    const thStyle = {
        fontFamily: "var(--font-m)",
        fontSize: 10.5,
        color: "var(--ink-faint)",
        padding: "4px 6px",
        textAlign: "center",
        borderBottom: "1px solid var(--line)"
    };
    const tdStyle = {
        padding: 4,
        textAlign: "center",
        verticalAlign: "middle"
    };