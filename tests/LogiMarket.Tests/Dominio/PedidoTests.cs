using LogiMarket.Domain.Common;
using LogiMarket.Domain.Devoluciones;
using LogiMarket.Domain.Pedidos;
using LogiMarket.Domain.Politicas;
using static LogiMarket.Tests.Dominio.CatalogoPrueba;

namespace LogiMarket.Tests.Dominio;

public class PedidoTests
{
    private readonly CatalogoPrueba _c = new();

    [Fact] // P02 (calculo): Ana VIP, KIT_TELETRABAJO x1, ESTANDAR
    public void P02_Calculo_Ana_total_59()
    {
        var p = new PedidoBuilder().ParaCliente(_c.Ana).ConEntrega(Estandar).AgregarLinea(_c.KitTeletrabajo, 1).Construir(Ahora);
        Assert.Equal((60.00m, 6.00m, 59.00m), (p.Subtotal, p.Descuento, p.Total));
        Assert.Equal("VIP", p.PoliticaDescuento);
    }

    [Fact] // P03 (calculo): Luis NORMAL, 2 P04 + 2 P05, EXPRES
    public void P03_Calculo_Luis_total_102()
    {
        var p = new PedidoBuilder().ParaCliente(_c.Luis).ConEntrega(Expres)
            .AgregarLinea(_c.P04, 2).AgregarLinea(_c.P05, 2).Construir(Ahora);
        Assert.Equal((90.00m, 0.00m, 102.00m), (p.Subtotal, p.Descuento, p.Total));
    }

    [Fact] // P04 (calculo): Carmen MAYORISTA, P03 x10
    public void P04_Calculo_Carmen_total_90()
    {
        var p = new PedidoBuilder().ParaCliente(_c.Carmen).ConEntrega(Estandar).AgregarLinea(_c.P03, 10).Construir(Ahora);
        Assert.Equal((100.00m, 15.00m, 90.00m), (p.Subtotal, p.Descuento, p.Total));
        Assert.Equal("MAYORISTA", p.PoliticaDescuento);
    }

    [Fact] // P13 Builder
    public void P13_Builder_rechaza_pedidos_incompletos()
    {
        Assert.Throws<DomainException>(() => new PedidoBuilder().ConEntrega(Estandar).AgregarLinea(_c.P01, 1).Construir(Ahora));
        Assert.Throws<DomainException>(() => new PedidoBuilder().ParaCliente(_c.Ana).AgregarLinea(_c.P01, 1).Construir(Ahora));
        Assert.Throws<DomainException>(() => new PedidoBuilder().ParaCliente(_c.Ana).ConEntrega(Estandar).Construir(Ahora));
    }

    [Fact] // P14 Prototype
    public void P14_Prototype_copia_profunda_no_altera_original()
    {
        var original = new PedidoBuilder().ParaCliente(_c.Ana).ConEntrega(Estandar).AgregarLinea(_c.KitTeletrabajo, 1).Construir(Ahora);
        var copia = original.Clonar(Ahora);
        copia.CambiarCantidad("KIT_TELETRABAJO", 3, CalculadoraDescuento.Predeterminada());

        Assert.Equal(1, original.Detalles[0].Cantidad);
        Assert.Equal(3, copia.Detalles[0].Cantidad);
        Assert.Equal(60.00m, original.Subtotal);
        Assert.Equal(180.00m, copia.Subtotal);
        Assert.NotSame(original.Detalles, copia.Detalles);
        Assert.NotSame(original.Detalles[0], copia.Detalles[0]);
        Assert.NotSame(original.Detalles[0].Componentes[0], copia.Detalles[0].Componentes[0]);
        Assert.Equal(0, copia.Id);
        Assert.Null(copia.ClaveIdempotencia);
    }

    [Fact] // P31 State: ciclo valido
    public void P31_State_ciclo_completo_registra_historial()
    {
        var p = new PedidoBuilder().ParaCliente(_c.Ana).ConEntrega(Estandar).AgregarLinea(_c.P01, 1).Construir(Ahora);
        p.Validar(Ahora);
        p.MarcarPagado("clave-1", Ahora);
        p.Despachar(Ahora);
        p.Entregar(Ahora);

        Assert.Equal(EstadoPedido.Entregado, p.Estado);
        Assert.Equal(new EstadoPedido[] { EstadoPedido.Borrador, EstadoPedido.Validado, EstadoPedido.Pagado, EstadoPedido.Despachado, EstadoPedido.Entregado },
            p.Historial.Select(h => h.Hacia));
    }

    [Fact] // P31 State: transiciones invalidas
    public void P31_State_rechaza_pagado_a_cancelado_y_estados_repetidos()
    {
        var p = new PedidoBuilder().ParaCliente(_c.Ana).ConEntrega(Estandar).AgregarLinea(_c.P01, 1).Construir(Ahora);
        Assert.Throws<TransicionInvalidaException>(() => p.Despachar(Ahora));
        p.Validar(Ahora);
        Assert.Throws<TransicionInvalidaException>(() => p.Validar(Ahora));
        p.MarcarPagado("clave-1", Ahora);
        Assert.Throws<TransicionInvalidaException>(() => p.Cancelar(Ahora));
        Assert.Throws<DomainException>(() => p.CambiarCantidad("P01", 2, CalculadoraDescuento.Predeterminada()));
        Assert.Equal(EstadoPedido.Pagado, p.Estado);
    }

    [Fact] // P32 Strategy
    public void P32_Strategy_descuentos_6_15_0_sin_acumular()
    {
        var calc = CalculadoraDescuento.Predeterminada();
        Assert.Equal(6.00m, calc.Calcular(60m, new(LogiMarket.Domain.Clientes.CategoriaCliente.Vip, 3)).Monto);
        Assert.Equal(15.00m, calc.Calcular(100m, new(LogiMarket.Domain.Clientes.CategoriaCliente.Normal, 10)).Monto);
        Assert.Equal(0.00m, calc.Calcular(90m, new(LogiMarket.Domain.Clientes.CategoriaCliente.Normal, 4)).Monto);
        // VIP + MAYORISTA: prioridad MAYORISTA, nunca 25 %.
        var ambos = calc.Calcular(100m, new(LogiMarket.Domain.Clientes.CategoriaCliente.Vip, 10));
        Assert.Equal(("MAYORISTA", 15.00m), (ambos.Politica, ambos.Monto));
    }

    [Fact] // P32 Strategy: extension sin editar la calculadora
    public void P32_Strategy_admite_nueva_politica_sin_modificar_calculadora()
    {
        var calc = new CalculadoraDescuento(new IPoliticaDescuento[] { new SinDescuento(), new Vip10(), new Mayorista15(), new PromoCincoPorCiento() });
        var r = calc.Calcular(100m, new(LogiMarket.Domain.Clientes.CategoriaCliente.Normal, 1));
        Assert.Equal(("PROMO5", 5.00m), (r.Politica, r.Monto));
    }

    [Fact] // P33 (regla pura de plazos)
    public void P33_Reglas_devolucion_defecto_dia20_ok_arrepentimiento_dia10_rechazado()
    {
        var entrega = Ahora;
        Assert.True(ReglasDevolucion.Evaluar(MotivoDevolucion.Defecto, entrega, entrega.AddDays(20), sinUso: false).Elegible);
        Assert.False(ReglasDevolucion.Evaluar(MotivoDevolucion.Arrepentimiento, entrega, entrega.AddDays(10), sinUso: true).Elegible);
        Assert.False(ReglasDevolucion.Evaluar(MotivoDevolucion.Arrepentimiento, entrega, entrega.AddDays(3), sinUso: false).Elegible);
    }

    [Fact] // P35 (calculo): reembolso sin tarifa de envio
    public void P35_Devolucion_Ana_reembolsa_54_sin_flete()
    {
        var p = new PedidoBuilder().ParaCliente(_c.Ana).ConEntrega(Estandar).AgregarLinea(_c.KitTeletrabajo, 1).Construir(Ahora);
        Assert.Throws<DomainException>(() => Devolucion.Para(p, MotivoDevolucion.Defecto, Ahora));
        p.Validar(Ahora); p.MarcarPagado("k", Ahora); p.Despachar(Ahora); p.Entregar(Ahora);
        Assert.Equal(54.00m, Devolucion.Para(p, MotivoDevolucion.Defecto, Ahora).Reembolso);
    }

    private sealed class PromoCincoPorCiento : IPoliticaDescuento
    {
        public string Nombre => "PROMO5";
        public decimal Tasa => 0.05m;
        public int Prioridad => 10;
        public bool Aplica(ContextoDescuento contexto) => true;
    }
}
