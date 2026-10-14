namespace RealFenixFailures.Domain.DTOs;

public static class FenixFailures {
    #region Air Conditioning System
    public static class AirConditioning {
        public static class CPC {
            public static string CPC_1 => "F_PNEUMATIC_CPC_1";
            public static string CPC_2 => "F_PNEUMATIC_CPC_2";
        }

        public static class Overheat {
            public static string Pack_1_overheat => "F_PNEUMATIC_PACK_1_OVERHEAT";
            public static string Pack_2_overheat => "F_PNEUMATIC_PACK_2_OVERHEAT";
        }

        public static class Regulator_fault {
            public static string Pack_1_regulator_fault => "F_PNEUMATIC_PACK_1_REG_FAULT";
            public static string Pack_2_regulator_fault => "F_PNEUMATIC_PACK_2_REG_FAULT";
        }

        public static class Zone_controller {
            public static string Zone_controller_primary => "F_PNEUMATIC_ZONECONTROLLER_PRIM";
            public static string Zone_controller_secondary => "F_PNEUMATIC_ZONECONTROLLER_SEC";
        }

        public static class Trim_air {
            public static string Hot_air_fault => "F_PNEUMATIC_TRIM_AIR";
        }

        public static class Cargo {
            public static string Cargo_ventilation_controller => "F_PNEUMATIC_CVC";
        }

        public static class Pneumatic_valves {
            public static string Hot_air_valve_aft_cargo => "F_PNEUMATIC_HOT_AIR_VALVE_AFT_CARGO";
        }

        public static class Cargo_vent {
            public static string Cargo_fwd_isolation_valve_upstream => "F_PNEUMATIC_CARGO_ISOL_FWD_UP";
            public static string Cargo_fwd_isolation_valve_downstream => "F_PNEUMATIC_CARGO_ISOL_FWD_DOWN";
            public static string Cargo_aft_isolation_valve_upstream => "F_PNEUMATIC_CARGO_ISOL_AFT_UP";
            public static string Cargo_aft_isolation_valve_downstream => "F_PNEUMATIC_CARGO_ISOL_AFT_DOWN";
        }

        public static class Recirculation {
            public static string Recirculation_fans => "F_PNEUMATIC_RECIRC_FANS";
        }

        public static class Decompression {
            public static string Slow_decompression => "F_PNEUMATIC_DECOMPRESSION_MINOR";
            public static string Rapid_decompression => "F_PNEUMATIC_DECOMPRESSION_MAJOR";
        }

        public static class Outflow_valve {
            public static string Outflow_valve_stuck => "F_PNEUMATIC_OUTFLOWVALVE_STUCK";
        }

        public static class Ventilation {
            public static string Ventilation_AEVC => "F_PNEUMATIC_AEVC";
            public static string Ventilation_blower_fault => "F_PNEUMATIC_BLOWER";
            public static string Ventilation_extract_fault => "F_PNEUMATIC_EXTRACT";
        }

        public static class Ventilation_valves {
            public static string Vent_inlet_valve => "F_PNEUMATIC_VENT_INLET";
            public static string Vent_extract_valve => "F_PNEUMATIC_VENT_EXTRACT";
        }
    }
    #endregion

    #region AutoFlight System
    public static class AutoFlight {
        public static class Nonresettable_fault {
            public static string FAC_1 => "F_OH_FLT_CTL_FAC_1";
            public static string FAC_2 => "F_OH_FLT_CTL_FAC_2";
        }

        public static class Resettable_fault {
            public static string FAC_1_resettable_fault => "F_OH_FLT_CTL_FAC_RECOVERABLE_1";
            public static string FAC_2_resettable_fault => "F_OH_FLT_CTL_FAC_RECOVERABLE_2";
        }

        public static class Rudder_travel_limiter {
            public static string Rudder_travel_limiter_channel_1 => "F_FC_YAW_RTL_1";
            public static string Rudder_travel_limiter_channel_2 => "F_FC_YAW_RTL_2";
        }

        public static class Rudder_trim {
            public static string Rudder_trim_channel_1 => "F_FC_RUDDERTRIM_1";
            public static string Rudder_trim_channel_2 => "F_FC_RUDDERTRIM_2";
        }

        public static class Reactive_W_S_det {
            public static string Reactive_W_S_det_channel_1 => "F_FC_WINDSHEAR_1";
            public static string Reactive_W_S_det_channel_2 => "F_FC_WINDSHEAR_2";
        }

        public static class Law_override {
            public static string Alternate_law_with_protection => "F_FC_ALTERNATE_LAW_1";
            public static string Alternate_law_without_protection => "F_FC_ALTERNATE_LAW_2";
            public static string Direct_law => "F_FC_DIRECT_LAW";
        }

        public static class FCU {
            public static string FCU_Channel_1 => "F_FCU_1";
            public static string FCU_Channel_2 => "F_FCU_2";
        }

        public static class A_THR {
            public static string A_THR_1 => "F_AUTOFLIGHT_ATHR1";
            public static string A_THR_2 => "F_AUTOFLIGHT_ATHR2";
        }

        public static class AP {
            public static string AP_1 => "F_AUTOFLIGHT_AP1";
            public static string AP_2 => "F_AUTOFLIGHT_AP2";
        }
    }
    #endregion

    #region Electrical power System
    public static class Electrical_power {
        public static class DC_Bus_Fault {
            public static string DC_Bat_bus => "F_ELEC_BUS_BAT";
            public static string DC_bus_1 => "F_ELEC_BUS_DC_1";
            public static string DC_bus_2 => "F_ELEC_BUS_DC_2";
            public static string Hot_bus_1 => "F_ELEC_BUS_HOT_1";
            public static string Hot_bus_2 => "F_ELEC_BUS_HOT_2";
            public static string DC_Essential_bus => "F_ELEC_BUS_DC_ESSENTIAL";
            public static string DC_Essential_shed_bus => "F_ELEC_BUS_DC_SHED";
        }

        public static class AC_Bus_Fault {
            public static string AC_bus_1 => "F_ELEC_BUS_AC_1";
            public static string AC_bus_2 => "F_ELEC_BUS_AC_2";
            public static string AC_Essential_bus => "F_ELEC_BUS_AC_ESSENTIAL";
            public static string AC_Essential_shed_bus => "F_ELEC_BUS_AC_SHED";
            public static string AC_Static_inverter_bus => "F_ELEC_BUS_AC_STATIC_INVERTER";
        }

        public static class AC_26v_Bus_Fault {
            public static string AC_bus_1_26v => "F_ELEC_BUS_AC26_1";
            public static string AC_bus_2_26v => "F_ELEC_BUS_AC26_2";
            public static string AC_ess_bus_26v => "F_ELEC_BUS_AC26_ESS";
        }

        public static class Generator {
            public static string APU_Generator => "F_ELEC_APU_GEN";
            public static string Generator_1 => "F_ELEC_DRIVE_FAILURE_L";
            public static string Generator_2 => "F_ELEC_DRIVE_FAILURE_R";
        }

        public static class IDG_oil_ovht {
            public static string IDG_1_oil_overheat => "F_ELEC_GEN_OVERHEAT_L";
            public static string IDG_2_oil_overheat => "F_ELEC_GEN_OVERHEAT_R";
        }

        public static class IDG_oil_low_pressure {
            public static string IDG_1_oil_low_pressure => "F_ELEC_DRIVE_OIL_LOW_L";
            public static string IDG_2_oil_low_pressure => "F_ELEC_DRIVE_OIL_LOW_R";
        }

        public static class Static_Inverter {
            public static string Static_inverter => "F_ELEC_STATIC_INVERTER";
        }

        public static class TR_Failure {
            public static string TR_1 => "F_ELEC_TR1";
            public static string TR_2 => "F_ELEC_TR2";
            public static string ESS_TR_failure => "F_ELEC_TR_ESS";
        }

        public static class AC_Ess {
            public static string AC_Ess_Bus_Altn => "F_ELEC_AC_ESS_ALTN";
        }

        public static class AC_Ess_feed {
            public static string AC_Ess_feed_from_AC_Bus_1 => "F_ELEC_AC_ESS_FEED_1";
            public static string AC_Ess_feed_from_AC_Bus_2 => "F_ELEC_AC_ESS_FEED_2";
        }
    }
    #endregion

    #region Fire protection System
    public static class Fire_protection {
        public static class SDCU {
            public static string F_SDCU => "F_SDCU";
        }

        public static class Fwd_cargo {
            public static string Fwd_cargo_smoke_detected => "F_FIRE_CARGO_FWD_SMOKE";
        }

        public static class Aft_cargo {
            public static string Aft_cargo_smoke_detected => "F_FIRE_CARGO_AFT_SMOKE";
        }

        public static class Lavatory {
            public static string Lavatory_smoke => "F_FIRE_LAVATORY_SMOKE";
        }

        public static class Fire_unextinguishable {
            public static string Eng_1_unextinguishable_fire => "F_OH_FIRE_ENG_1";
            public static string Eng_2_unextinguishable_fire => "F_OH_FIRE_ENG_2";
            public static string APU_unextinguishable_fire => "F_OH_FIRE_APU";
        }

        public static class Fire_1_bottle {
            public static string Eng_1_fire_extinguished_with_1_bottle => "F_OH_FIRE_ENG_1_1BOTTLE";
            public static string Eng_2_fire_extinguished_with_1_bottle => "F_OH_FIRE_ENG_2_1BOTTLE";
            public static string APU_fire_extinguished_with_1_bottle => "F_OH_FIRE_APU_1BOTTLE";
        }

        public static class Fire_2_bottles {
            public static string Eng_1_fire_extinguished_with_2_bottles => "F_OH_FIRE_ENG_1_2BOTTLES";
            public static string Eng_2_fire_extinguished_with_2_bottles => "F_OH_FIRE_ENG_2_2BOTTLES";
        }

        public static class Loop_Eng_1 {
            public static string Eng_1_Loop_A => "F_OH_FIRE_ENG1_LOOP_A";
            public static string Eng_1_Loop_B => "F_OH_FIRE_ENG1_LOOP_B";
        }

        public static class Loop_Eng_2 {
            public static string Eng_2_Loop_A => "F_OH_FIRE_ENG2_LOOP_A";
            public static string Eng_2_Loop_B => "F_OH_FIRE_ENG2_LOOP_B";
        }

        public static class Loop_APU {
            public static string APU_Loop_A => "F_OH_FIRE_AP2_LOOP_A";
            public static string APU_Loop_B => "F_OH_FIRE_APU_LOOP_B";
        }

        public static class FDU {
            public static string FDU_1 => "F_FIRE_FDU1";
            public static string FDU_2 => "F_FIRE_FDU2";
            public static string FDU_3 => "F_FIRE_FDU3";
        }

        public static class Avionics {
            public static string Avionics_smoke_detected => "F_FIRE_AVIONICS_SMOKE";
        }
    }
    #endregion

    #region Fuel System
    public static class Fuel {
        public static class FQI {
            public static string FQI_Chan_1 => "F_FUEL_FQI1";
            public static string FQI_Chan_2 => "F_FUEL_FQI2";
        }

        public static class Fuel_pump_left {
            public static string Fuel_pump_left_1 => "F_FUEL_PUMP_LEFT_1";
            public static string Fuel_pump_left_2 => "F_FUEL_PUMP_LEFT_2";
        }

        public static class Fuel_pump_center {
            public static string Fuel_pump_center_1 => "F_FUEL_PUMP_CENTER_1";
            public static string Fuel_pump_center_2 => "F_FUEL_PUMP_CENTER_2";
        }

        public static class Fuel_pump_right {
            public static string Fuel_pump_right_1 => "F_FUEL_PUMP_RIGHT_1";
            public static string Fuel_pump_right_2 => "F_FUEL_PUMP_RIGHT_2";
        }

        public static class Fuel_temp_high {
            public static string Inner_tank_high_fuel_temp_ECAM => "F_FUEL_HIGH_TEMP_INNER";
            public static string Outer_tank_high_fuel_temp_ECAM => "F_FUEL_HIGH_TEMP_OUTER";
            public static string Inner_tank_high_fuel_temp_Adv => "F_FUEL_HIGH_TEMP_INNER_ADV";
            public static string Outer_tank_high_fuel_temp_Adv => "F_FUEL_HIGH_TEMP_OUTER_ADV";
        }

        public static class Fuel_temp_low {
            public static string Inner_tank_low_fuel_temp_ECAM => "F_FUEL_LOW_TEMP_INNER";
            public static string Outer_tank_low_fuel_temp_ECAM => "F_FUEL_LOW_TEMP_OUTER";
            public static string Inner_tank_low_fuel_temp_Adv => "F_FUEL_LOW_TEMP_INNER_ADV";
            public static string Outer_tank_low_fuel_temp_Adv => "F_FUEL_LOW_TEMP_OUTER_ADV";
        }

        public static class Fuel_transfer_valves {
            public static string Left_transfer_1 => "F_FUEL_XFER_VALVE_1_L";
            public static string Left_transfer_2 => "F_FUEL_XFER_VALVE_2_L";
            public static string Right_transfer_1 => "F_FUEL_XFER_VALVE_1_R";
            public static string Right_transfer_2 => "F_FUEL_XFER_VALVE_2_R";
        }

        public static class Fuel_valves {
            public static string Eng_1_fuel_valve_stuck => "F_FUEL_ENGINE_1_VALVE";
            public static string Eng_2_fuel_valve_stuck => "F_FUEL_ENGINE_2_VALVE";
            public static string Crossfeed_valve => "F_FUEL_VALVE_CROSSFEED";
            public static string Defuel_Transfer_Valve => "F_DEFUEL_TRANSFER_VALVE";
        }

        public static class Fuel_leak {
            public static string Fuel_leak_Left_outer => "F_FUEL_LEAK_LEFT_OUTER";
            public static string Fuel_leak_Left_inner => "F_FUEL_LEAK_LEFT_INNER";
            public static string Fuel_leak_Center => "F_FUEL_LEAK_CENTER";
            public static string Fuel_leak_Right_inner => "F_FUEL_LEAK_RIGHT_INNER";
            public static string Fuel_leak_Right_outer => "F_FUEL_LEAK_RIGHT_OUTER";
        }

        public static class HP_Fuel_valve {
            public static string Eng_1_hp_fuel_valve => "F_ENG1_HP_VALVE";
            public static string Eng_2_hp_fuel_valve => "F_ENG2_HP_VALVE";
        }
    }
    #endregion

    #region Hydraulic power System
    public static class Hydraulic_power {
        public static class Hydraulic_leak {
            public static string Green_hydraulic_leak => "F_HYD_LEAK_GREEN";
            public static string Blue_hydraulic_leak => "F_HYD_LEAK_BLUE";
            public static string Yellow_hydraulic_leak => "F_HYD_LEAK_YELLOW";
        }

        public static class Reservoir_overheat {
            public static string Green_reservoir_overheat => "F_HYD_RSVR_OVERHEAT_GREEN";
            public static string Blue_reservoir_overheat => "F_HYD_RSVR_OVERHEAT_BLUE";
            public static string Yellow_reservoir_overheat => "F_HYD_RSVR_OVERHEAT_YELLOW";
        }

        public static class Reservoir_low_air_pressure {
            public static string Green_low_reservoir_air_pressure => "F_HYD_RSVR_AIR_PRESSURE_GREEN";
            public static string Blue_low_reservoir_air_pressure => "F_HYD_RSVR_AIR_PRESSURE_BLUE";
            public static string Yellow_low_reservoir_air_pressure => "F_HYD_RSVR_AIR_PRESSURE_YELLOW";
        }

        public static class Hydraulic_low {
            public static string Green_hydraulic_low_level => "F_HYD_LOW_GREEN";
            public static string Blue_hydraulic_low_level => "F_HYD_LOW_BLUE";
            public static string Yellow_hydraulic_low_level => "F_HYD_LOW_YELLOW";
        }

        public static class Engine_pumps {
            public static string Eng_1_pump_failure => "F_HYD_PUMP_ENG_1";
            public static string Eng_2_pump_failure => "F_HYD_PUMP_ENG_2";
        }

        public static class Fire_valves {
            public static string Eng_1_fire_valve => "F_HYD_FIRE_VALVE_1";
            public static string Eng_2_fire_valve => "F_HYD_FIRE_VALVE_2";
        }

        public static class PTU {
            public static string PTU_Fault => "F_HYD_PTU";
        }

        public static class Elec_pumps {
            public static string Elec_hyd_pump_Blue_failure => "F_HYD_PUMP_BLUE";
            public static string Elec_hyd_pump_Yellow_failure => "F_HYD_PUMP_YELLOW";
        }

        public static class Elec_pump_overheat {
            public static string Elec_blue_pump_overheat => "F_HYD_PUMP_OVERHEAT_BLUE";
            public static string Elec_yellow_pump_overheat => "F_HYD_PUMP_OVERHEAT_YELLOW";
        }
    }
    #endregion

    #region Landing gear System
    public static class Landing_gear {
        public static class BSCU {
            public static string BSCU_Sys_1 => "F_BSCU_1";
            public static string BSCU_Sys_2 => "F_BSCU_2";
        }

        public static class ABCU {
            public static string F_ABCU => "F_BRAKE_ABCU";
        }

        public static class Brake_fault {
            public static string BSCU_1_brake_fault => "F_HYD_BSCU_1";
            public static string BSCU_2_brake_fault => "F_HYD_BSCU_2";
            public static string Autobrake_failure => "F_BRAKE_AUTOBRAKE";
        }

        public static class Wheel_brake_fault {
            public static string Wheel_brake_fault_1 => "F_BRAKE_WHEEL_1";
            public static string Wheel_brake_fault_2 => "F_BRAKE_WHEEL_2";
            public static string Wheel_brake_fault_3 => "F_BRAKE_WHEEL_3";
            public static string Wheel_brake_fault_4 => "F_BRAKE_WHEEL_4";
        }

        public static class Tyre_pressure {
            public static string Tyre_pressure_main_1_low => "F_GEAR_TYRE_PSI_MAIN_1";
            public static string Tyre_pressure_main_2_low => "F_GEAR_TYRE_PSI_MAIN_2";
            public static string Tyre_pressure_left_1_low => "F_GEAR_TYRE_PSI_LEFT_1";
            public static string Tyre_pressure_left_2_low => "F_GEAR_TYRE_PSI_LEFT_2";
            public static string Tyre_pressure_right_1_low => "F_GEAR_TYRE_PSI_RIGHT_1";
            public static string Tyre_pressure_right_2_low => "F_GEAR_TYRE_PSI_RIGHT_2";
        }

        public static class Steering {
            public static string Nose_wheel_steering => "F_BRAKE_NWS";
        }

        public static class LGCIU {
            public static string LGCIU_1 => "F_MISC_LGCIU1";
            public static string LGCIU_2 => "F_MISC_LGCIU2";
        }

        public static class Hydraulics_valves {
            public static string Gear_safety_valve => "F_GEAR_SAFETY_VALVE";
        }

        public static class Gear_lock {
            public static string Left_main_gear_does_not_lock => "F_GEAR_LOCK_LEFT";
            public static string Nose_gear_does_not_lock => "F_GEAR_LOCK_NOSE";
            public static string Right_main_gear_does_not_lock => "F_GEAR_LOCK_RIGHT";
        }

        public static class Gear_failure {
            public static string Gear_locked_up => "F_GEAR_LOCKED_UP";
            public static string Gear_locked_down => "F_GEAR_LOCKED_DOWN";
        }
    }
    #endregion

    #region Navigation System
    public static class Navigation {
        public static class IR_fault {
            public static string IR1_IR_failure => "F_OH_NAV_IR_TOTAL_1";
            public static string IR2_IR_failure => "F_OH_NAV_IR_TOTAL_2";
            public static string IR3_IR_failure => "F_OH_NAV_IR_TOTAL_3";
        }

        public static class IRS_position_failure {
            public static string IR1_position_failure => "F_OH_NAV_IR_POSITION_1";
            public static string IR2_position_failure => "F_OH_NAV_IR_POSITION_2";
            public static string IR3_position_failure => "F_OH_NAV_IR_POSITION_3";
        }

        public static class ADR_fault {
            public static string ADR1_failure => "F_OH_NAV_ADR_ADR1";
            public static string ADR2_failure => "F_OH_NAV_ADR_ADR2";
            public static string ADR3_failure => "F_OH_NAV_ADR_ADR3";
        }

        public static class IRS_Alignment {
            public static string IR1_alignment => "F_OH_NAV_IR_ALIGNMENT_1";
            public static string IR2_alignment => "F_OH_NAV_IR_ALIGNMENT_2";
            public static string IR3_alignment => "F_OH_NAV_IR_ALIGNMENT_3";
        }

        public static class Pitot_blocked {
            public static string Pitot_blocked_Capt => "F_OH_NAV_PITOT_BLOCKED_1";
            public static string Pitot_blocked_F_O => "F_OH_NAV_PITOT_BLOCKED_2";
            public static string Pitot_blocked_Stdby => "F_OH_NAV_PITOT_BLOCKED_3";
        }

        public static class Pitch_discrepancy {
            public static string IR1_pitch_discrepancy => "F_NAV_IR1_PITCH_DISCREPANCY";
            public static string IR2_pitch_discrepancy => "F_NAV_IR2_PITCH_DISCREPANCY";
            public static string IR3_pitch_discrepancy => "F_NAV_IR3_PITCH_DISCREPANCY";
        }

        public static class Bank_discrepancy {
            public static string IR1_bank_discrepancy => "F_NAV_IR1_BANK_DISCREPANCY";
            public static string IR2_bank_discrepancy => "F_NAV_IR2_BANK_DISCREPANCY";
            public static string IR3_bank_discrepancy => "F_NAV_IR3_BANK_DISCREPANCY";
        }

        public static class Heading_discrepancy {
            public static string IR1_heading_discrepancy => "F_NAV_IR1_HDG_DISCREPANCY";
            public static string IR2_heading_discrepancy => "F_NAV_IR2_HDG_DISCREPANCY";
            public static string IR3_heading_discrepancy => "F_NAV_IR3_HDG_DISCREPANCY";
        }

        public static class IR_Disagree {
            public static string IR1_IR_Disagree => "F_NAV_IR_DISAGREE1";
            public static string IR2_IR_Disagree => "F_NAV_IR_DISAGREE2";
        }

        public static class MCDU {
            public static string MCDU_1 => "F_MCDU_1";
            public static string MCDU_2 => "F_MCDU_2";
        }

        public static class MCDU_recoverable_fault {
            public static string CDU_1_recoverable_fault => "F_MCDU_1_RECOVERABLE";
            public static string CDU_2_recoverable_fault => "F_MCDU_2_RECOVERABLE";
        }

        public static class FMGC {
            public static string FMGC_1 => "F_FMGC_1";
            public static string FMGC_2 => "F_FMGC_2";
        }

        public static class GPWC {
            public static string F_GPWC => "F_GPWS";
        }

        public static class Radios {
            public static string VOR_1 => "F_NAV_VOR1";
            public static string VOR_2 => "F_NAV_VOR2";
            public static string ADF_1 => "F_NAV_ADF1";
            public static string ADF_2 => "F_NAV_ADF2";
        }

        public static class ILS {
            public static string ILS_1_LOC => "F_NAV_ILS1_LOC";
            public static string ILS_1_G_S => "F_NAV_ILS1_GS";
            public static string ILS_2_LOC => "F_NAV_ILS2_LOC";
            public static string ILS_2_G_S => "F_NAV_ILS2_GS";
        }

        public static class ILS_transmitter {
            public static string Localizer => "F_NAV_LOC";
            public static string Glideslope => "F_NAV_GS";
        }

        public static class GPS {
            public static string GPS_1 => "F_NAV_GPS1";
            public static string GPS_2 => "F_NAV_GPS2";
        }

        public static class Radio_altimeter {
            public static string Radio_altimeter_1 => "F_NAV_RALT1";
            public static string Radio_altimeter_2 => "F_NAV_RALT2";
        }

        public static class NAV {
            public static string NAV_accuracy_downgrade => "F_NAV_ACCURACY_DOWNGRADE";
        }

        public static class TCAS {
            public static string F_TCAS => "F_NAV_TCAS";
        }

        public static class ATC {
            public static string ATC_1 => "F_NAV_ATC1";
            public static string ATC_2 => "F_NAV_ATC2";
        }
    }
    #endregion

    #region Pneumatic System
    public static class Pneumatic {
        public static class Lavatory_Galley_Fan {
            public static string Lavatory_Galley_fan => "F_PNEUMATIC_LAV_GAL_FAN";
        }

        public static class Pneumatic_valves {
            public static string Eng_1_HP_Valve => "F_PNEUMATIC_HP_VALVE_1";
            public static string Eng_2_HP_Valve => "F_PNEUMATIC_HP_VALVE_2";
            public static string Ram_air_valve => "F_PNEUMATIC_RAM_AIR_VALVE";
            public static string Eng_1_bleed_valve => "F_PNEUMATIC_BLEED_VALVE_1";
            public static string Eng_2_bleed_valve => "F_PNEUMATIC_BLEED_VALVE_2";
            public static string Hot_air_valve => "F_PNEUMATIC_HOT_AIR_VALVE";
        }

        public static class Bleed_air_low_temperature {
            public static string Eng_1_bleed_low_temp => "F_ENG1_BLEED_LOW_TEMP";
            public static string Eng_2_bleed_low_temp => "F_ENG2_BLEED_LOW_TEMP";
        }

        public static class BMC {
            public static string BMC_1 => "F_PNEUMATIC_BMC_1";
            public static string BMC_2 => "F_PNEUMATIC_BMC_2";
        }

        public static class Pneumatic_valve {
            public static string Crossbleed_valve => "F_PNEUMATIC_CROSS_BLEED_VALVE";
            public static string APU_Bleed_valve => "F_PNEUMATIC_APU_VALVE";
        }

        public static class Pack_valves {
            public static string Pack_1_flow_control_valve => "F_PNEUMATIC_PACK_VALVE_1";
            public static string Pack_2_flow_control_valve => "F_PNEUMATIC_PACK_VALVE_2";
        }

        public static class Bleed_leak {
            public static string Bleed_leak_wing_engine_1 => "F_PNEUMATIC_LEAK_WING_ENG1";
            public static string Bleed_leak_wing_engine_2 => "F_PNEUMATIC_LEAK_WING_ENG2";
            public static string Bleed_leak_pylon_engine_1 => "F_PNEUMATIC_LEAK_PYLON_ENG1";
            public static string Bleed_leak_pylon_engine_2 => "F_PNEUMATIC_LEAK_PYLON_ENG2";
            public static string Bleed_leak_APU => "F_PNEUMATIC_LEAK_APU";
        }
    }
    #endregion

    #region APU System
    public static class APU {
        public static class ECB {
            public static string F_ECB => "F_ELEC_APU_ECB";
        }

        public static class Oil {
            public static string APU_Low_oil_level => "F_APU_LOW_OIL";
        }

        public static class Fuel_valves {
            public static string APU_fuel_valve => "F_FUEL_VALVE_APU";
        }
    }
    #endregion

    #region Communications System
    public static class Communications {
        public static class Stuck_mic {
            public static string Capt_stuck_mic => "F_COMM_STUCK_PTT_CAPT";
            public static string F_O_stuck_mic => "F_COMM_STUCK_PTT_FO";
        }
    }
    #endregion

    #region Ice_and_rain_protection System
    public static class Ice_and_rain_protection {
        public static class WAI_Valves {
            public static string Left_WAI_Valve => "F_PNEUMATIC_WAI_1";
            public static string Right_WAI_Valve => "F_PNEUMATIC_WAI_2";
        }

        public static class EAI_Valves {
            public static string Eng_1_EAI_Valve => "F_PNEUMATIC_EAI_1";
            public static string Eng_2_EAI_Valve => "F_PNEUMATIC_EAI_2";
        }

        public static class WHC {
            public static string WHC_1 => "F_ICE_WHC_1";
            public static string WHC_2 => "F_ICE_WHC_2";
        }

        public static class AOA_Heat {
            public static string AOA_heat_Capt => "F_ICE_AOA_HEAT_CPT";
            public static string AOA_heat_F_O => "F_ICE_AOA_FO";
            public static string AOA_heat_STBY => "F_ICE_AOA_STBY";
        }

        public static class TAT_Heat {
            public static string TAT_heat_Capt => "F_ICE_TAT_HEAT_CPT";
            public static string TAT_heat_F_O => "F_ICE_TAT_FO";
            public static string TAT_heat_STBY => "F_ICE_TAT_STBY";
        }

        public static class Pitot_Heat {
            public static string Pitot_heat_Capt => "F_ICE_PITOT_HEAT_CPT";
            public static string Pitot_heat_F_O => "F_ICE_PITOT_FO";
            public static string Pitot_heat_STBY => "F_ICE_PITOT_STBY";
        }

        public static class Static_Heat {
            public static string Left_Static_heat_Capt => "F_ICE_STAT_HEAT_CPT_L";
            public static string Right_Static_heat_Capt => "F_ICE_STAT_HEAT_CPT_R";
            public static string Left_Static_heat_F_O => "F_ICE_STAT_FO_L";
            public static string Right_Static_heat_F_O => "F_ICE_STAT_FO_R";
            public static string Left_Static_heat_Stby => "F_ICE_STAT_STBY_L";
            public static string Right_Static_heat_Stby => "F_ICE_STAT_STBY_R";
        }

        public static class PHC {
            public static string PHC_1 => "F_ICE_PHC1";
            public static string PHC_2 => "F_ICE_PHC2";
            public static string PHC_3 => "F_ICE_PHC3";
        }

        public static class Icing {
            public static string Icing_eng_1 => "F_ICING_ENG1";
            public static string Icing_eng_2 => "F_ICING_ENG2";
        }
    }
    #endregion

    #region Flight_controls System
    public static class Flight_controls {
        public static class IPPU {
            public static string Ippu1 => "F_FC_IPPU1";
            public static string Ippu2 => "F_FC_IPPU2";
        }

        public static class Surface_lock {
            public static string Flap_locked => "F_HYD_WTB_FLAP";
            public static string Slat_locked => "F_HYD_WTB_SLAT";
        }

        public static class Flaps {
            public static string Alignment_fault => "F_HYD_FSCC_ALIGNMENT";
        }

        public static class SFCC {
            public static string SFCC_1 => "F_OH_FLT_CLT_SFCC_1";
            public static string SFCC_2 => "F_OH_FLT_CLT_SFCC_2";
        }

        public static class Flap {
            public static string SFCC_1_flap_sys => "F_SFCC_1_FLAP";
            public static string SFCC_2_flap_sys => "F_SFCC_2_FLAP";
        }

        public static class Slat {
            public static string SFCC_1_slat_sys => "F_SFCC_1_SLAT";
            public static string SFCC_2_slat_sys => "F_SFCC_2_SLAT";
        }

        public static class Nonresettable_fault {
            public static string ELAC_1 => "F_OH_FLT_CTL_ELAC_1";
            public static string ELAC_2 => "F_OH_FLT_CTL_ELAC_2";
            public static string SEC_1 => "F_OH_FLT_CTL_SEC_1";
            public static string SEC_2 => "F_OH_FLT_CTL_SEC_2";
            public static string SEC_3 => "F_OH_FLT_CTL_SEC_3";
        }

        public static class Resettable_fault {
            public static string ELAC_1_resettable_fault => "F_OH_FLT_CTL_ELAC_RECOVERABLE_1";
            public static string ELAC_2_resettable_fault => "F_OH_FLT_CTL_ELAC_RECOVERABLE_2";
            public static string SEC_1_resettable_fault => "F_OH_FLT_CTL_SEC_RECOVERABLE_1";
            public static string SEC_2_resettable_fault => "F_OH_FLT_CTL_SEC_RECOVERABLE_2";
            public static string SEC_3_resettable_fault => "F_OH_FLT_CTL_SEC_RECOVERABLE_3";
        }

        public static class Yaw_damper {
            public static string Yaw_damper_channel_1 => "F_FC_YAW_DAMPER_1";
            public static string Yaw_damper_channel_2 => "F_FC_YAW_DAMPER_2";
        }

        public static class Sidestick_reversal {
            public static string Sidestick_reversal_pitch_Capt => "F_FC_SIDESTICK_REV_PITCH_CAPT";
            public static string Sidestick_reversal_roll_Capt => "F_FC_SIDESTICK_REV_ROLL_CAPT";
            public static string Sidestick_reversal_pitch_F_O => "F_FC_SIDESTICK_REV_PITCH_FO";
            public static string Sidestick_reversal_roll_F_O => "F_FC_SIDESTICK_REV_ROLL_FO";
        }

        public static class Sidestick {
            public static string Sidestick_fault_Capt => "F_FC_SIDESTICK_FAULT_CAPT";
            public static string Sidestick_fault_F_O => "F_FC_SIDESTICK_FAULT_FO";
        }

        public static class FCDC {
            public static string FCDC_1 => "B_INT_SFCDC1F";
            public static string FCDC_2 => "B_INT_SFCDC2F";
        }

        public static class Stabilizer {
            public static string Stabilizer_jam => "F_FCTL_STAB_JAM";
        }

        public static class Elev {
            public static string Elev_L_R_failure => "F_FCTL_ELEV_LR";
            public static string L_Elev_failure => "F_FCTL_ELEV_L";
            public static string R_Elev_failure => "F_FCTL_ELEV_R";
        }
    }
    #endregion

    #region Start_faults System
    public static class Start_faults {
        public static class Start_valve {
            public static string Start_valve_left => "F_PNEUMATIC_START_VALVE_1";
            public static string Start_valve_right => "F_PNEUMATIC_START_VALVE_2";
        }

        public static class Hot_start {
            public static string Eng_1_hot_start => "F_START_FAULT_1_HOT_START";
            public static string Eng_2_hot_start => "F_START_FAULT_2_HOT_START";
        }

        public static class Stall_Hung_start {
            public static string Eng_1_hung_start => "F_START_FAULT_1_HUNG_START";
            public static string Eng_2_hung_start => "F_START_FAULT_2_HUNG_START";
        }

        public static class No_ignition {
            public static string Eng_1_no_ignition => "F_START_FAULT_1_NO_IGNITION";
            public static string Eng_2_no_ignition => "F_START_FAULT_2_NO_IGNITION";
        }
    }
    #endregion

    #region Indicating_Recording_system System
    public static class Indicating_Recording_system {
        public static class CFDIU {
            public static string F_CFDIU => "F_CFDIU";
        }

        public static class ECP {
            public static string F_ECP => "F_ECP";
        }

        public static class SDAC {
            public static string SDAC_1 => "F_SDAC_1";
            public static string SDAC_2 => "F_SDAC_2";
        }

        public static class FWC {
            public static string FWC_1 => "F_FWC1";
            public static string FWC_2 => "F_FWC2";
        }

        public static class CVR {
            public static string F_CVR => "F_CVR";
        }

        public static class DMC {
            public static string DMC_1 => "F_DISPLAY_DMC_1";
            public static string DMC_2 => "F_DISPLAY_DMC_2";
            public static string DMC_3 => "F_DISPLAY_DMC_3";
        }

        public static class DCDU {
            public static string DCDU_Capt => "F_DISPLAY_DCDU_CAPT";
            public static string DCDU_FO => "F_DISPLAY_DCDU_FO";
        }

        public static class DU {
            public static string DU_Capt_PFD => "F_DISPLAY_DU_CAPTAIN_OUT";
            public static string DU_Capt_ND => "F_DISPLAY_DU_CAPTAIN_IN";
            public static string DU_F_O_ND => "F_DISPLAY_DU_FO_IN";
            public static string DU_F_O_PFD => "F_DISPLAY_DU_FO_OUT";
            public static string DU_ECAM_Upper => "F_DISPLAY_DU_ECAM_UPPER";
            public static string DU_ECAM_Lower => "F_DISPLAY_DU_ECAM_LOWER";
        }
    }
    #endregion

    #region Oxygen System
    public static class Oxygen {
        public static class Crew_oxygen_1 {
            public static string Crew_oxygen_pressure_400 => "F_OXYGEN_CREW_LOW";
            public static string Crew_oxygen_pressure_800 => "F_OXYGEN_CREW_MED";
        }

        public static class Crew_oxygen_2 {
            public static string Crew_oxygen_2_pressure_400 => "F_OXYGEN_CREW2_LOW";
            public static string Crew_oxygen_2_pressure_800 => "F_OXYGEN_CREW2_MED";
        }

        public static class Crew_oxygen {
            public static string Oxygen_supply_valve => "F_OXYGEN_CREW_SUPPLY_VALVE";
            public static string Oxygen_supply_valve_2 => "F_OXYGEN_CREW_SUPPLY_VALVE2";
        }
    }
    #endregion

    #region Information_systems System
    public static class Information_systems {
        public static class ATSU {
            public static string F_ATSU => "F_INFO_ATSU";
        }
    }
    #endregion

    #region Doors System
    public static class Doors {
        public static class Doors_left {
            public static string Forward_avionics_door => "F_DOOR_AVIONICS_1";
            public static string Left_avionics_door => "F_DOOR_AVIONICS_2";
            public static string Right_avionics_door => "F_DOOR_AVIONICS_3";
            public static string Forward_entry_left_door => "F_DOOR_FWD_ENTRY_LEFT";
            public static string Left_wing_1_door => "F_DOOR_LEFT_WING_1";
            public static string Left_wing_2_door => "F_DOOR_LEFT_WING_2";
            public static string Aft_entry_left_door => "F_DOOR_AFT_ENTRY_LEFT";
        }

        public static class Doors_right {
            public static string Forward_entry_right_door => "F_DOOR_FWD_ENTRY_RIGHT";
            public static string Aft_avionics_door => "F_DOOR_AVIONICS_4";
            public static string Forward_cargo_door => "F_DOOR_FWD_CARGO";
            public static string Right_wing_1_door => "F_DOOR_RIGHT_WING_1";
            public static string Right_wing_2_door => "F_DOOR_RIGHT_WING_2";
            public static string Aft_cargo_door => "F_DOOR_AFT_CARGO";
            public static string Bulk_door => "F_DOOR_BULK";
            public static string Aft_entry_right_door => "F_DOOR_AFT_ENTRY_RIGHT";
        }
    }
    #endregion

    #region Power_plant System
    public static class Power_plant {
        public static class Fadec {
            public static string Fadec_Left_Channel_A => "F_ENGINE_FADEC_LEFT_A";
            public static string Fadec_Left_Channel_B => "F_ENGINE_FADEC_LEFT_B";
            public static string Fadec_Right_Channel_A => "F_ENGINE_FADEC_RIGHT_A";
            public static string Fadec_Right_Channel_B => "F_ENGINE_FADEC_RIGHT_B";
        }

        public static class EIU {
            public static string EIU_1 => "F_ENG1_EIU";
            public static string EIU_2 => "F_ENG2_EIU";
        }

        public static class Engine_surge {
            public static string Left_surge => "F_ENGINE_1_SURGE";
            public static string Right_surge => "F_ENGINE_2_SURGE";
        }

        public static class Engine_failure {
            public static string Eng_1_failure => "F_ENGINE_1";
            public static string Eng_2_failure => "F_ENGINE_2";
        }

        public static class Engine_failure_with_damage {
            public static string Eng_1_failure_with_damage => "F_ENGINE_1_DAMAGE";
            public static string Eng_2_failure_with_damage => "F_ENGINE_2_DAMAGE";
        }

        public static class Engine_birdstrike {
            public static string Left_bird_strike => "F_ENGINE_1_BIRDSTRIKE";
            public static string Right_bird_strike => "F_ENGINE_2_BIRDSTRIKE";
        }

        public static class Reverser_pressurized {
            public static string Left_reverser_pressurized => "F_REV_PRESS_ENG_1";
            public static string Right_reverser_pressurized => "F_REV_PRESS_ENG_2";
        }

        public static class Reverser_unlocked {
            public static string Left_reverser_unlocked => "F_REV_UNLOCK_ENG_1";
            public static string Right_reverser_unlocked => "F_REV_UNLOCK_ENG_2";
        }

        public static class Reverser_inhibited_by_maintenance {
            public static string Left_reverser_inhibited_by_maint => "F_REV_INHIBIT_ENG_1";
            public static string Right_reverser_inhibited_by_maint => "F_REV_INHIBIT_ENG_2";
        }

        public static class vibration_N1 {
            public static string Eng_1_high_vibration_N1 => "F_VIB_N1_ENG_1";
            public static string Eng_2_high_vibration_N1 => "F_VIB_N1_ENG_2";
        }

        public static class vibration_N2 {
            public static string Eng_1_high_vibration_N2 => "F_VIB_N2_ENG_1";
            public static string Eng_2_high_vibration_N2 => "F_VIB_N2_ENG_2";
        }

        public static class Oil_leak {
            public static string Left_oil_leak => "F_ENG1_OIL_LEAK";
            public static string Right_oil_leak => "F_ENG2_OIL_LEAK";
        }

        public static class Reverser_shutoff_valve {
            public static string Reverser_shutoff_valve_left => "F_HYD_REVERSER_SHUTOFF_VALVE_1";
            public static string Reverser_shutoff_valve_right => "F_HYD_REVERSER_SHUTOFF_VALVE_2";
        }
    }
    #endregion

    #region Utility Methods
    /// <summary>
    /// Obtiene todos los IDs de fallas disponibles
    /// </summary>
    /// <returns>Lista de todos los IDs de fallas</returns>
    public static List<string> GetAllFailureIds() {
        var ids = new List<string>();

        // Reflection para obtener todos los valores constantes de las clases anidadas
        var fields = typeof(FenixFailures).GetFields(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.FlattenHierarchy);

        foreach (var field in fields) {
            if (field.IsLiteral && !field.IsInitOnly) {
                if (field.GetValue(null) is string id) {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>
    /// Verifica si un ID de fallo existe
    /// </summary>
    /// <param name="fenixId">ID de fallo de Fenix</param>
    /// <returns>True si el ID existe, false en caso contrario</returns>
    public static bool Exists(string fenixId) {
        return GetAllFailureIds().Contains(fenixId);
    }
    #endregion
}

