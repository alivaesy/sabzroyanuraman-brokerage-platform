/* tslint:disable */
/* eslint-disable */

export class Point {
    free(): void;
    [Symbol.dispose](): void;
    constructor(x: number, y: number, z: number);
    x: number;
    y: number;
    z: number;
}

export class Projection {
    free(): void;
    [Symbol.dispose](): void;
    constructor(defn: string);
    readonly axis: string;
    readonly isGeocentric: boolean;
    readonly isLatlon: boolean;
    readonly isNormalizedAxis: boolean;
    readonly projName: string;
    readonly to_meter: number;
    readonly units: string;
}

export function add_nadgrid(key: string, array: Uint8Array): void;

/**
 * Parse and validate a .rgplugin package using RustGIS' own package and manifest validator.
 */
export function inspect_plugin_package(bytes: Uint8Array): string;

/**
 * Install only after the UI has explicitly approved every permission declared in the manifest.
 */
export function install_plugin_package(bytes: Uint8Array, approved_permissions_json: string): string;

export function list_installed_plugins(): string;

export function output_nodata_value(): number;

/**
 * Run a plugin command inside RustGIS' wasmi sandbox, with fuel and memory limits.
 * NoHost intentionally exposes no project editing, file-system or network capabilities.
 */
export function run_installed_plugin_command(id: string, params_json: string): string;

export function run_raster_operation(operation: string, width: number, height: number, origin_x: number, pixel_width: number, origin_y: number, pixel_height: number, epsg: number, no_data: number, bands: Array<any>, expression: string, azimuth: number, altitude: number, z_factor: number): Float32Array;

export function set_installed_plugin_enabled(id: string, enabled: boolean): void;

export function transform(src: Projection, dst: Projection, point: Point): void;

export function uninstall_plugin(id: string): void;

export type InitInput = RequestInfo | URL | Response | BufferSource | WebAssembly.Module;

export interface InitOutput {
    readonly memory: WebAssembly.Memory;
    readonly __wbg_get_point_x: (a: number) => number;
    readonly __wbg_get_point_y: (a: number) => number;
    readonly __wbg_get_point_z: (a: number) => number;
    readonly __wbg_point_free: (a: number, b: number) => void;
    readonly __wbg_projection_free: (a: number, b: number) => void;
    readonly __wbg_set_point_x: (a: number, b: number) => void;
    readonly __wbg_set_point_y: (a: number, b: number) => void;
    readonly __wbg_set_point_z: (a: number, b: number) => void;
    readonly add_nadgrid: (a: number, b: number, c: any) => [number, number];
    readonly inspect_plugin_package: (a: any) => [number, number, number, number];
    readonly install_plugin_package: (a: any, b: number, c: number) => [number, number, number, number];
    readonly list_installed_plugins: () => [number, number];
    readonly output_nodata_value: () => number;
    readonly point_new: (a: number, b: number, c: number) => number;
    readonly projection_axis: (a: number) => [number, number];
    readonly projection_isGeocentric: (a: number) => number;
    readonly projection_isLatlon: (a: number) => number;
    readonly projection_isNormalizedAxis: (a: number) => number;
    readonly projection_new: (a: number, b: number) => [number, number, number];
    readonly projection_projName: (a: number) => [number, number];
    readonly projection_to_meter: (a: number) => number;
    readonly projection_units: (a: number) => [number, number];
    readonly run_installed_plugin_command: (a: number, b: number, c: number, d: number) => [number, number, number, number];
    readonly run_raster_operation: (a: number, b: number, c: number, d: number, e: number, f: number, g: number, h: number, i: number, j: number, k: any, l: number, m: number, n: number, o: number, p: number) => [number, number, number];
    readonly set_installed_plugin_enabled: (a: number, b: number, c: number) => [number, number];
    readonly transform: (a: number, b: number, c: number) => [number, number];
    readonly uninstall_plugin: (a: number, b: number) => [number, number];
    readonly rust_zstd_wasm_shim_calloc: (a: number, b: number) => number;
    readonly rust_zstd_wasm_shim_free: (a: number) => void;
    readonly rust_zstd_wasm_shim_malloc: (a: number) => number;
    readonly rust_zstd_wasm_shim_memcmp: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_memcpy: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_memmove: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_memset: (a: number, b: number, c: number) => number;
    readonly rust_zstd_wasm_shim_qsort: (a: number, b: number, c: number, d: number) => void;
    readonly __wbindgen_externrefs: WebAssembly.Table;
    readonly __wbindgen_malloc: (a: number, b: number) => number;
    readonly __wbindgen_realloc: (a: number, b: number, c: number, d: number) => number;
    readonly __wbindgen_free: (a: number, b: number, c: number) => void;
    readonly __externref_table_dealloc: (a: number) => void;
    readonly __wbindgen_start: () => void;
}

export type SyncInitInput = BufferSource | WebAssembly.Module;

/**
 * Instantiates the given `module`, which can either be bytes or
 * a precompiled `WebAssembly.Module`.
 *
 * @param {{ module: SyncInitInput }} module - Passing `SyncInitInput` directly is deprecated.
 *
 * @returns {InitOutput}
 */
export function initSync(module: { module: SyncInitInput } | SyncInitInput): InitOutput;

/**
 * If `module_or_path` is {RequestInfo} or {URL}, makes a request and
 * for everything else, calls `WebAssembly.instantiate` directly.
 *
 * @param {{ module_or_path: InitInput | Promise<InitInput> }} module_or_path - Passing `InitInput` directly is deprecated.
 *
 * @returns {Promise<InitOutput>}
 */
export default function __wbg_init (module_or_path?: { module_or_path: InitInput | Promise<InitInput> } | InitInput | Promise<InitInput>): Promise<InitOutput>;
