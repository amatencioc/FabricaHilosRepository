# Copilot Instructions

## General Guidelines
- When the user asks to make validation "equal" or "the same" as another field's validation, they mean the validation MESSAGE TEXT should follow the same format/pattern, NOT that the validation logic should change.
- Use hardcoded emoji icons for dashboard charts instead of dynamic/generic solutions to enhance visual richness, even if data changes slightly.

## Data Interpretation Rules
- In the UBIGEO table:
  - When PAIS='01', it means Peru (NOM_DPT=departamento, NOM_DTT=distrito).
  - When PAIS≠'01' and EXPORTACION='N', NOM_DPT is a foreign country name and NOM_DTT is the city/district of that country (purchased in Peru).
  - When PAIS≠'01' and EXPORTACION='S', NOM_DPT is a foreign country name and NOM_DTT is the city/district of that country (export).
- In the CLIENTES table, C.PAIS='PE' represents Peru.