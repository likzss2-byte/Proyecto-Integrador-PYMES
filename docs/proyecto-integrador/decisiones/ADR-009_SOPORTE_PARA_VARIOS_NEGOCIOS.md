# ADR-009: Soporte para varios negocios

Fecha: 20 de julio de 2026  
Versión relacionada: 0.8  
Estado: Parcial

## Contexto

El requisito pedía permitir el uso por diferentes negocios.

## Problema

Teníamos que separar datos sin tener una base distinta por cada negocio.

## Opciones

- Una base por negocio.
- Una base con `business_id`.
- Servidor multiempresa.

## Decisión

Usar `business_id` en tablas principales y un servicio de negocio predeterminado.

## Razón

La base final contiene `businesses` y referencias por negocio. Los servicios reciben `businessId`.

## Consecuencias

El modelo queda preparado. La interfaz todavía no permite administración completa de negocios.

## Riesgos

Que se interprete como multiempresa completo cuando solo está parcialmente implementado.

## Evidencia

`DatabaseMigrator.cs`, `BusinessService.cs`, servicios y repositorios que reciben `businessId`.

