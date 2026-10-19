//! #93 direct-core diagnostics; no managed/FFI acceptance claim.
use oxidize_pdf::forms::calculation_system::{
    EventType, FormCalculationSystem, JavaScriptCalculation, SimpleOperation,
};
use oxidize_pdf::forms::calculations::{
    Calculation, CalculationEngine, CalculationFunction, FieldValue,
};

fn sum(field: &str) -> JavaScriptCalculation {
    JavaScriptCalculation::SimpleCalculate {
        operation: SimpleOperation::Sum,
        fields: vec![field.into()],
    }
}

#[test]
fn lower_engine_two_hop_values_and_order_control() {
    let mut engine = CalculationEngine::new();
    engine.set_field_value("a", FieldValue::Number(2.0));
    for (target, source) in [("b", "a"), ("c", "b")] {
        engine
            .add_calculation(
                target,
                Calculation::Function(CalculationFunction::Sum(vec![source.into()])),
            )
            .unwrap();
    }
    engine.set_field_value("a", FieldValue::Number(7.0));
    assert_eq!(engine.get_field_value("b"), Some(&FieldValue::Number(7.0)));
    assert_eq!(engine.get_field_value("c"), Some(&FieldValue::Number(7.0)));
    assert_eq!(engine.get_summary().calculation_order, vec!["b", "c"]);
    assert!(engine
        .add_calculation(
            "a",
            Calculation::Function(CalculationFunction::Sum(vec!["c".into()]))
        )
        .is_err());
}

#[test]
fn system_direct_dependency_and_cycle_control() {
    let mut system = FormCalculationSystem::new();
    system
        .set_field_value("a", FieldValue::Number(2.0))
        .unwrap();
    system.add_js_calculation("b", sum("a")).unwrap();
    system.clear_events();
    system
        .set_field_value("a", FieldValue::Number(7.0))
        .unwrap();
    assert_eq!(system.get_summary().events_logged, 2);
    assert!(system.add_js_calculation("a", sum("b")).is_err());
}

#[test]
fn system_two_hop_recalculation_emits_both_calculation_events() {
    let mut system = FormCalculationSystem::new();
    system
        .set_field_value("a", FieldValue::Number(2.0))
        .unwrap();
    system.add_js_calculation("b", sum("a")).unwrap();
    system.add_js_calculation("c", sum("b")).unwrap();
    system.clear_events();
    system
        .set_field_value("a", FieldValue::Number(7.0))
        .unwrap();
    // One ValueChanged plus a CalculationTriggered for each dependent.
    assert_eq!(system.get_summary().events_logged, 3);
    assert_eq!(system.get_field_value("b"), Some(&FieldValue::Number(7.0)));
    assert_eq!(system.get_field_value("c"), Some(&FieldValue::Number(7.0)));
    let events = system.get_recent_events(3);
    assert_eq!(
        events.iter().map(|e| e.field()).collect::<Vec<_>>(),
        vec!["a", "b", "c"]
    );
    assert_eq!(events[0].event_type(), &EventType::ValueChanged);
    assert_eq!(events[0].old_value(), Some(&FieldValue::Number(2.0)));
    assert_eq!(events[0].new_value(), Some(&FieldValue::Number(7.0)));
    assert!(events[0].timestamp() <= events[2].timestamp());
}
