# Caught action generation review

- Attempt 01 was rejected by strict QC: eight source cells touched the processor's trimmed boundary. The full raw sheet and pipeline metadata are preserved in `attempt-01/`.
- Attempt 02 was rejected: the smaller character was inconsistent with the shared Rook scale profile (body-scale CV 0.226; profile drift 0.459) and still registered 17 source-edge frames. It is preserved in `attempt-02/`.
- Accepted attempt 03 used the 6x8 square layout guide and the approved Rook identity reference. Strict QC reports 48/48 valid frames, body-scale CV 0.042, anchor-Y std 0.022, profile drift 0.040, zero output-edge contact, zero empty frames, and zero paste clamping. Six source cells register contact only at the processor's 4px trimmed boundary; the raw cells retain that 4px margin and the processed preview shows complete feet. The processor's documented `--allow-source-edge-touch` exception is recorded in the final pipeline metadata and runtime `action.json`.
