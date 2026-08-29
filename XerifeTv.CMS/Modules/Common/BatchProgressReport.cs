namespace XerifeTv.CMS.Modules.Common;

/// <summary>
/// Relatorio de progresso emitido pelas operacoes em lote para atualizar o job
/// na fila de processamento em segundo plano.
/// </summary>
public readonly record struct BatchProgressReport(int Processed, int Total, int Successful);
